using Memoa.Replay;

namespace Memoa.Replay.Api;

/// <summary>
/// Configuration options for the Memoa replay REST API endpoints.
/// </summary>
public sealed class MemoaReplayApiOptions
{
    /// <summary>
    /// The configuration section name. Default: <c>"Memoa:Replay"</c>.
    /// </summary>
    public const string SectionName = "Memoa:Replay";

    /// <summary>
    /// The route prefix for replay endpoints.
    /// Default: <c>"/replay"</c>.
    /// </summary>
    public string RoutePrefix { get; set; } = "/replay";

    /// <summary>
    /// The name of an ASP.NET Core authorization policy to require on all replay endpoints.
    /// When <c>null</c>, no policy-based authorization is applied.
    /// </summary>
    public string? AuthorizationPolicy { get; set; }

    /// <summary>
    /// The name of the header used for API key authentication.
    /// Default: <c>"X-Api-Key"</c>.
    /// </summary>
    public string ApiKeyHeaderName { get; set; } = "X-Api-Key";

    /// <summary>
    /// The expected API key value. When set, requests must include a matching header.
    /// When <c>null</c>, API key authentication is not applied.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// The base URL to replay requests against.
    /// When <c>null</c>, requests are replayed against the current application.
    /// </summary>
    public string? TargetBaseUrl { get; set; }

    /// <summary>
    /// The default timeline mode for replay sessions.
    /// Default: <see cref="TimelineMode.None"/>.
    /// </summary>
    public TimelineMode DefaultTimelineMode { get; set; } = TimelineMode.None;

    /// <summary>
    /// Maximum allowed parallelism for client-requested replay sessions.
    /// Default: 10.
    /// </summary>
    public int MaxParallelism { get; set; } = 10;
}
