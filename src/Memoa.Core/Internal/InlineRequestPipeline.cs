using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Memoa.Internal;

/// <summary>
/// Pipeline that writes captured requests to sinks inline (synchronously with the HTTP pipeline).
/// </summary>
internal sealed class InlineRequestPipeline : IRequestPipeline
{
    private readonly IEnumerable<IRequestSink> _sinks;
    private readonly ILogger<InlineRequestPipeline> _logger;

    public InlineRequestPipeline(IEnumerable<IRequestSink> sinks, ILogger<InlineRequestPipeline> logger)
    {
        _sinks = sinks;
        _logger = logger;
    }

    public async ValueTask SubmitAsync(RecordedRequest request, CancellationToken cancellationToken)
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
}
