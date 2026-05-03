using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Memoa.Internal;

/// <summary>
/// Pipeline that queues captured requests in a <see cref="Channel{T}"/> and writes them
/// to sinks via background workers, minimizing impact on request latency.
/// </summary>
internal sealed class BackgroundRequestPipeline : IRequestPipeline, IHostedService, IDisposable
{
    private readonly Channel<RecordedRequest> _channel;
    private readonly IEnumerable<IRequestSink> _sinks;
    private readonly ILogger<BackgroundRequestPipeline> _logger;
    private readonly MemoaPipelineOptions _options;
    private readonly CancellationTokenSource _cts = new();
    private readonly List<Task> _workers = [];

    public BackgroundRequestPipeline(
        IEnumerable<IRequestSink> sinks,
        IOptions<MemoaOptions> options,
        ILogger<BackgroundRequestPipeline> logger)
    {
        _sinks = sinks;
        _logger = logger;
        _options = options.Value.Pipeline;

        var channelOptions = new BoundedChannelOptions(_options.ChannelCapacity)
        {
            SingleWriter = false,
            SingleReader = _options.WorkerCount == 1,
            FullMode = _options.FullMode switch
            {
                ChannelFullMode.Wait => BoundedChannelFullMode.Wait,
                ChannelFullMode.DropOldest => BoundedChannelFullMode.DropOldest,
                ChannelFullMode.DropWrite => BoundedChannelFullMode.DropWrite,
                _ => BoundedChannelFullMode.DropWrite
            }
        };

        _channel = Channel.CreateBounded<RecordedRequest>(channelOptions);
    }

    public ValueTask SubmitAsync(RecordedRequest request, CancellationToken cancellationToken)
    {
        if (_channel.Writer.TryWrite(request))
        {
            MemoaDiagnostics.ChannelQueueSize.Add(1);
            return ValueTask.CompletedTask;
        }

        // If TryWrite failed and mode is Wait, use async write
        if (_options.FullMode == ChannelFullMode.Wait)
        {
            return WriteWithWaitAsync(request, cancellationToken);
        }

        // Otherwise the item was dropped by the channel
        MemoaDiagnostics.RequestsDropped.Add(1);
        _logger.LogWarning("Dropped captured request {RequestId} due to full channel", request.Id);
        return ValueTask.CompletedTask;
    }

    private async ValueTask WriteWithWaitAsync(RecordedRequest request, CancellationToken cancellationToken)
    {
        await _channel.Writer.WriteAsync(request, cancellationToken).ConfigureAwait(false);
        MemoaDiagnostics.ChannelQueueSize.Add(1);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var workerCount = Math.Max(1, _options.WorkerCount);

        _logger.LogInformation("Starting {WorkerCount} Memoa background pipeline worker(s)", workerCount);

        for (var i = 0; i < workerCount; i++)
        {
            var workerId = i;
            _workers.Add(Task.Run(() => ProcessAsync(workerId, _cts.Token), _cts.Token));
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Memoa background pipeline, draining channel...");

        _channel.Writer.TryComplete();

        using var timeoutCts = new CancellationTokenSource(_options.ShutdownTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            await Task.WhenAll(_workers).WaitAsync(linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Memoa background pipeline shutdown timed out after {Timeout}", _options.ShutdownTimeout);
        }

        await _cts.CancelAsync().ConfigureAwait(false);
    }

    private async Task ProcessAsync(int workerId, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Memoa background worker {WorkerId} started", workerId);

        try
        {
            await foreach (var request in _channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                MemoaDiagnostics.ChannelQueueSize.Add(-1);
                await WriteSinksAsync(request, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Memoa background worker {WorkerId} crashed", workerId);
        }

        _logger.LogDebug("Memoa background worker {WorkerId} stopped", workerId);
    }

    private async Task WriteSinksAsync(RecordedRequest request, CancellationToken cancellationToken)
    {
        foreach (var sink in _sinks)
        {
            using var activity = MemoaDiagnostics.ActivitySource.StartActivity("memoa.sink.write");
            activity?.SetTag("memoa.sink.type", sink.GetType().Name);
            activity?.SetTag("memoa.request.id", request.Id.ToString());

            var sw = Stopwatch.StartNew();
            try
            {
                await sink.WriteAsync(request, cancellationToken).ConfigureAwait(false);
                sw.Stop();

                MemoaDiagnostics.RequestsWritten.Add(1);
                MemoaDiagnostics.SinkWriteDuration.Record(sw.Elapsed.TotalMilliseconds,
                    new KeyValuePair<string, object?>("memoa.sink.type", sink.GetType().Name));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                sw.Stop();
                MemoaDiagnostics.RequestsFailed.Add(1);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

                _logger.LogError(ex, "Failed to write request {RequestId} to sink {SinkType}",
                    request.Id, sink.GetType().Name);
            }
        }
    }

    public void Dispose()
    {
        _cts.Dispose();
    }
}
