namespace Memoa.Replay;

/// <summary>
/// Options controlling how captured requests are replayed.
/// </summary>
public sealed class ReplayOptions
{
    /// <summary>
    /// The timeline mode controlling inter-request timing.
    /// Default: <see cref="TimelineMode.None"/>.
    /// </summary>
    public TimelineMode Mode { get; set; } = TimelineMode.None;

    /// <summary>
    /// Number of concurrent requests when <see cref="Mode"/> is <see cref="TimelineMode.None"/>.
    /// Ignored in <see cref="TimelineMode.Relative"/> mode (always sequential).
    /// Default: 1.
    /// </summary>
    public int Parallelism { get; set; } = 1;

    /// <summary>
    /// Fixed delay in milliseconds between requests when <see cref="Mode"/> is <see cref="TimelineMode.None"/>.
    /// Default: 0 (no delay).
    /// </summary>
    public int DelayMs { get; set; }

    /// <summary>
    /// When <c>true</c>, requests are logged but not actually sent.
    /// </summary>
    public bool DryRun { get; set; }

    /// <summary>
    /// The base URL to replay requests against (e.g., <c>https://localhost:5001</c>).
    /// </summary>
    public string? TargetBaseUrl { get; set; }
}
