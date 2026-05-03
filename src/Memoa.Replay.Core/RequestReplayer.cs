using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Memoa.Replay;

/// <summary>
/// Replays captured HTTP requests against a target server,
/// optionally reproducing the original timeline between requests.
/// </summary>
public sealed class RequestReplayer
{
    /// <summary>
    /// Custom header added to every replayed request to indicate it originated from the Memoa replay engine.
    /// </summary>
    public const string ReplayHeader = "X-Memoa-Replay";

    private readonly HttpClient _httpClient;
    private readonly ReplayOptions _options;
    private readonly ILogger<RequestReplayer> _logger;

    public RequestReplayer(HttpClient httpClient, ReplayOptions options, ILogger<RequestReplayer> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Replays all requests from the given source, respecting the configured timeline and parallelism options.
    /// </summary>
    /// <param name="requests">An async stream of captured requests to replay.</param>
    /// <param name="onOutcome">Optional callback invoked after each request is replayed (or skipped in dry-run mode).</param>
    /// <param name="cancellationToken">A token to cancel the replay.</param>
    /// <returns>A summary of the replay session.</returns>
    public async Task<ReplayResult> ReplayAsync(
        IAsyncEnumerable<RecordedRequest> requests,
        Action<ReplayOutcome>? onOutcome,
        CancellationToken cancellationToken)
    {
        var sorted = await CollectAndSortAsync(requests, cancellationToken).ConfigureAwait(false);

        if (sorted.Count == 0)
        {
            return new ReplayResult(0, 0, 0);
        }

        return _options.Mode switch
        {
            TimelineMode.Relative => await ReplayRelativeAsync(sorted, onOutcome, cancellationToken).ConfigureAwait(false),
            _ => await ReplayParallelAsync(sorted, onOutcome, cancellationToken).ConfigureAwait(false)
        };
    }

    private async Task<ReplayResult> ReplayRelativeAsync(
        List<RecordedRequest> sorted,
        Action<ReplayOutcome>? onOutcome,
        CancellationToken cancellationToken)
    {
        var total = sorted.Count;
        var succeeded = 0;
        var failed = 0;

        for (var i = 0; i < sorted.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (i > 0)
            {
                var delta = sorted[i].CapturedAtUtc - sorted[i - 1].CapturedAtUtc;
                if (delta > TimeSpan.Zero)
                {
                    _logger.LogDebug("Timeline delay: {Delay}ms before request {RequestId}", delta.TotalMilliseconds, sorted[i].Id);
                    await Task.Delay(delta, cancellationToken).ConfigureAwait(false);
                }
            }

            var outcome = await ReplaySingleAsync(sorted[i], cancellationToken).ConfigureAwait(false);
            if (outcome.Success)
            {
                succeeded++;
            }
            else
            {
                failed++;
            }

            onOutcome?.Invoke(outcome);
        }

        return new ReplayResult(total, succeeded, failed);
    }

    private async Task<ReplayResult> ReplayParallelAsync(
        List<RecordedRequest> sorted,
        Action<ReplayOutcome>? onOutcome,
        CancellationToken cancellationToken)
    {
        var total = sorted.Count;
        var succeeded = 0;
        var failed = 0;

        var semaphore = new SemaphoreSlim(Math.Max(1, _options.Parallelism));
        var tasks = new List<Task>();

        foreach (var request in sorted)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var outcome = await ReplaySingleAsync(request, cancellationToken).ConfigureAwait(false);
                    if (outcome.Success)
                    {
                        Interlocked.Increment(ref succeeded);
                    }
                    else
                    {
                        Interlocked.Increment(ref failed);
                    }

                    onOutcome?.Invoke(outcome);
                }
                finally
                {
                    semaphore.Release();
                }

                if (_options.DelayMs > 0)
                {
                    await Task.Delay(_options.DelayMs, cancellationToken).ConfigureAwait(false);
                }
            }, cancellationToken));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        return new ReplayResult(total, succeeded, failed);
    }

    private async Task<ReplayOutcome> ReplaySingleAsync(RecordedRequest request, CancellationToken cancellationToken)
    {
        if (_options.DryRun)
        {
            _logger.LogInformation("[DRY-RUN] {Method} {Path}{QueryString}", request.Method, request.Path, request.QueryString ?? "");
            return new ReplayOutcome(request, true, null, null);
        }

        try
        {
            using var message = BuildHttpRequestMessage(request);
            using var response = await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("Replayed {Method} {Path} → {StatusCode}", request.Method, request.Path, (int)response.StatusCode);
            return new ReplayOutcome(request, true, (int)response.StatusCode, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to replay {Method} {Path} ({RequestId})", request.Method, request.Path, request.Id);
            return new ReplayOutcome(request, false, null, ex.Message);
        }
    }

    private static HttpRequestMessage BuildHttpRequestMessage(RecordedRequest request)
    {
        var method = new HttpMethod(request.Method);
        var uri = $"{request.Path}{request.QueryString ?? ""}";
        var message = new HttpRequestMessage(method, uri);

        // Add replay indicator header
        message.Headers.TryAddWithoutValidation(ReplayHeader, "true");

        if (request.Headers is not null)
        {
            foreach (var (key, values) in request.Headers)
            {
                if (key.StartsWith("Content-", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("Host", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                message.Headers.TryAddWithoutValidation(key, values);
            }
        }

        if (request.Body is not null)
        {
            if (request.Body.Text is not null)
            {
                message.Content = new StringContent(request.Body.Text, Encoding.UTF8);
            }
            else if (request.Body.Base64Bytes is not null)
            {
                message.Content = new ByteArrayContent(Convert.FromBase64String(request.Body.Base64Bytes));
            }

            if (message.Content is not null && request.Body.ContentType is not null)
            {
                message.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(request.Body.ContentType);
            }
        }

        return message;
    }

    private static async Task<List<RecordedRequest>> CollectAndSortAsync(
        IAsyncEnumerable<RecordedRequest> requests,
        CancellationToken cancellationToken)
    {
        var list = new List<RecordedRequest>();

        await foreach (var request in requests.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            list.Add(request);
        }

        list.Sort((a, b) => a.CapturedAtUtc.CompareTo(b.CapturedAtUtc));
        return list;
    }
}
