using Memoa.Replay;

namespace Memoa.Replay.Api;

/// <summary>
/// Request body for <c>POST /replay/run</c> to trigger a replay session.
/// </summary>
public sealed class ReplayRunRequest
{
    /// <summary>
    /// Only replay requests captured at or after this UTC time.
    /// </summary>
    public DateTimeOffset? From { get; set; }

    /// <summary>
    /// Only replay requests captured at or before this UTC time.
    /// </summary>
    public DateTimeOffset? To { get; set; }

    /// <summary>
    /// Glob pattern to filter request paths.
    /// </summary>
    public string? PathPattern { get; set; }

    /// <summary>
    /// HTTP methods to include (e.g., ["GET", "POST"]).
    /// </summary>
    public string[]? Methods { get; set; }

    /// <summary>
    /// Maximum number of requests to replay.
    /// </summary>
    public int? Take { get; set; }

    /// <summary>
    /// Timeline mode override. When <c>null</c>, uses the server default.
    /// </summary>
    public TimelineMode? TimelineMode { get; set; }

    /// <summary>
    /// Parallelism override. When <c>null</c>, uses the server default.
    /// </summary>
    public int? Parallelism { get; set; }

    /// <summary>
    /// Target base URL override. When <c>null</c>, uses the server default.
    /// </summary>
    public string? TargetBaseUrl { get; set; }

    /// <summary>
    /// When <c>true</c>, requests are logged but not sent.
    /// </summary>
    public bool DryRun { get; set; }

    /// <summary>
    /// Bearer token for target authentication. Overrides the server default.
    /// </summary>
    public string? AuthBearerToken { get; set; }
}
