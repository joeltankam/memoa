namespace Memoa.Replay;

/// <summary>
/// Controls how the replay engine times the delivery of captured requests.
/// </summary>
public enum TimelineMode
{
    /// <summary>
    /// Fire requests as fast as possible, respecting <see cref="ReplayOptions.Parallelism"/>
    /// and optional <see cref="ReplayOptions.DelayMs"/> between requests.
    /// </summary>
    None = 0,

    /// <summary>
    /// Preserve the relative timing between consecutive captured requests.
    /// Requests are replayed sequentially, waiting the exact delta between their original capture timestamps.
    /// </summary>
    Relative = 1
}
