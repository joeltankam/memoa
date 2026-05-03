using System.Collections.Concurrent;
using Memoa.Replay;
using Microsoft.Extensions.Logging;

namespace Memoa.Replay.Api.Internal;

internal sealed class ReplayJobTracker
{
    private readonly ConcurrentDictionary<Guid, ReplayJobState> _jobs = new();
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IRequestSource _requestSource;
    private readonly MemoaReplayApiOptions _options;
    private readonly ILogger<ReplayJobTracker> _logger;

    public ReplayJobTracker(
        IHttpClientFactory httpClientFactory,
        IRequestSource requestSource,
        MemoaReplayApiOptions options,
        ILogger<ReplayJobTracker> logger)
    {
        _httpClientFactory = httpClientFactory;
        _requestSource = requestSource;
        _options = options;
        _logger = logger;
    }

    public ReplayJobInfo StartJob(ReplayRunRequest request)
    {
        var jobId = Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow;

        var state = new ReplayJobState
        {
            JobId = jobId,
            Status = "Running",
            StartedAtUtc = startedAt
        };

        _jobs[jobId] = state;

        _ = Task.Run(() => ExecuteJobAsync(jobId, request));

        return new ReplayJobInfo
        {
            JobId = jobId,
            Status = state.Status,
            StartedAtUtc = startedAt
        };
    }

    public ReplayJobInfo? GetJob(Guid jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var state))
        {
            return null;
        }

        return new ReplayJobInfo
        {
            JobId = state.JobId,
            Status = state.Status,
            StartedAtUtc = state.StartedAtUtc,
            CompletedAtUtc = state.CompletedAtUtc,
            Result = state.Result
        };
    }

    private async Task ExecuteJobAsync(Guid jobId, ReplayRunRequest request)
    {
        try
        {
            var targetBaseUrl = request.TargetBaseUrl ?? _options.TargetBaseUrl;
            var httpClient = _httpClientFactory.CreateClient("MemoaReplay");
            if (!string.IsNullOrEmpty(targetBaseUrl))
            {
                httpClient.BaseAddress = new Uri(targetBaseUrl);
            }

            var timelineMode = request.TimelineMode ?? _options.DefaultTimelineMode;
            var parallelism = Math.Min(request.Parallelism ?? 1, _options.MaxParallelism);

            var replayOptions = new ReplayOptions
            {
                Mode = timelineMode,
                Parallelism = parallelism,
                DryRun = request.DryRun,
                TargetBaseUrl = targetBaseUrl
            };

            var replayer = new RequestReplayer(httpClient, replayOptions, _logger as ILogger<RequestReplayer> ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<RequestReplayer>.Instance);

            var query = new RequestQuery
            {
                From = request.From,
                To = request.To,
                PathPattern = request.PathPattern,
                Methods = request.Methods,
                Take = request.Take
            };

            var result = await replayer.ReplayAsync(
                _requestSource.ReadAsync(query, CancellationToken.None),
                onOutcome: null,
                CancellationToken.None).ConfigureAwait(false);

            if (_jobs.TryGetValue(jobId, out var state))
            {
                state.Status = "Completed";
                state.CompletedAtUtc = DateTimeOffset.UtcNow;
                state.Result = result;
            }

            _logger.LogInformation("Replay job {JobId} completed: {Total} total, {Succeeded} succeeded, {Failed} failed",
                jobId, result.Total, result.Succeeded, result.Failed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Replay job {JobId} failed", jobId);

            if (_jobs.TryGetValue(jobId, out var state))
            {
                state.Status = "Failed";
                state.CompletedAtUtc = DateTimeOffset.UtcNow;
                state.Result = new ReplayResult(0, 0, 0);
            }
        }
    }

    internal sealed class ReplayJobState
    {
        public required Guid JobId { get; init; }
        public required string Status { get; set; }
        public required DateTimeOffset StartedAtUtc { get; init; }
        public DateTimeOffset? CompletedAtUtc { get; set; }
        public ReplayResult? Result { get; set; }
    }
}
