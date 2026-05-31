using System.Collections.Concurrent;
using Memoa.Replay;
using Memoa.Replay.Authentication;
using Microsoft.Extensions.Logging;

namespace Memoa.Replay.Api.Internal;

internal sealed class ReplayJobTracker
{
    private readonly ConcurrentDictionary<Guid, ReplayJobState> _jobs = new();
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IRequestSource _requestSource;
    private readonly MemoaReplayApiOptions _options;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<ReplayJobTracker> _logger;

    public ReplayJobTracker(
        IHttpClientFactory httpClientFactory,
        IRequestSource requestSource,
        MemoaReplayApiOptions options,
        ILoggerFactory loggerFactory)
    {
        _httpClientFactory = httpClientFactory;
        _requestSource = requestSource;
        _options = options;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<ReplayJobTracker>();
    }

    public ReplayJobInfo StartJob(ReplayRunRequest request)
    {
        var jobId = Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow;
        var cts = new CancellationTokenSource();

        var state = new ReplayJobState
        {
            JobId = jobId,
            Status = "Running",
            StartedAtUtc = startedAt,
            CancellationSource = cts
        };

        _jobs[jobId] = state;

        _ = Task.Run(() => ExecuteJobAsync(jobId, request, cts.Token));

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

    public bool CancelJob(Guid jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var state))
        {
            return false;
        }

        if (state.Status != "Running")
        {
            return false;
        }

        state.CancellationSource.Cancel();
        return true;
    }

    private async Task ExecuteJobAsync(Guid jobId, ReplayRunRequest request, CancellationToken cancellationToken)
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
                TargetBaseUrl = targetBaseUrl,
                Authentication = BuildAuthentication(request)
            };

            var replayerLogger = _loggerFactory.CreateLogger<RequestReplayer>();
            var replayer = new RequestReplayer(httpClient, replayOptions, replayerLogger);

            var query = new RequestQuery
            {
                From = request.From,
                To = request.To,
                PathPattern = request.PathPattern,
                Methods = request.Methods,
                Take = request.Take
            };

            var result = await replayer.ReplayAsync(
                _requestSource.ReadAsync(query, cancellationToken),
                onOutcome: null,
                cancellationToken).ConfigureAwait(false);

            if (_jobs.TryGetValue(jobId, out var state))
            {
                state.Status = "Completed";
                state.CompletedAtUtc = DateTimeOffset.UtcNow;
                state.Result = result;
            }

            _logger.LogInformation("Replay job {JobId} completed: {Total} total, {Succeeded} succeeded, {Failed} failed",
                jobId, result.Total, result.Succeeded, result.Failed);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Replay job {JobId} was cancelled", jobId);

            if (_jobs.TryGetValue(jobId, out var state))
            {
                state.Status = "Cancelled";
                state.CompletedAtUtc = DateTimeOffset.UtcNow;
            }
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

    private ReplayAuthentication? BuildAuthentication(ReplayRunRequest request)
    {
        // New Authentication object takes precedence
        if (request.Authentication is not null)
        {
            var auth = request.Authentication;
            var result = new ReplayAuthentication();

            if (auth.OAuthClientCredentials is not null)
            {
                result.OAuthClientCredentials = auth.OAuthClientCredentials;
            }
            else if (!string.IsNullOrEmpty(auth.BearerToken))
            {
                result.BearerToken = auth.BearerToken;
            }
            else if (!string.IsNullOrEmpty(auth.HeaderName) && !string.IsNullOrEmpty(auth.HeaderValue))
            {
                result.HeaderName = auth.HeaderName;
                result.HeaderValue = auth.HeaderValue;
            }

            return result;
        }

        // Legacy: AuthBearerToken shorthand (backward compat)
        if (!string.IsNullOrEmpty(request.AuthBearerToken))
        {
            return new ReplayAuthentication { BearerToken = request.AuthBearerToken };
        }

        return _options.TargetAuthentication;
    }

    internal sealed class ReplayJobState
    {
        public required Guid JobId { get; init; }
        public required string Status { get; set; }
        public required DateTimeOffset StartedAtUtc { get; init; }
        public required CancellationTokenSource CancellationSource { get; init; }
        public DateTimeOffset? CompletedAtUtc { get; set; }
        public ReplayResult? Result { get; set; }
    }
}
