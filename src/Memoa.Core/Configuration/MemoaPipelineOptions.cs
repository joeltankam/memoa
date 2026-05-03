namespace Memoa;

/// <summary>
/// Defines the pipeline mode for delivering captured requests to sinks.
/// </summary>
public enum PipelineMode
{
    /// <summary>
    /// Requests are queued in a background channel and written asynchronously.
    /// Minimizes impact on request latency.
    /// </summary>
    Background,

    /// <summary>
    /// Requests are written to sinks inline, before the response is returned.
    /// Simpler but adds latency to every captured request.
    /// </summary>
    Inline
}

/// <summary>
/// Defines the behavior when the background channel is full.
/// </summary>
public enum ChannelFullMode
{
    /// <summary>
    /// Wait for space to become available.
    /// </summary>
    Wait,

    /// <summary>
    /// Drop the oldest item in the channel.
    /// </summary>
    DropOldest,

    /// <summary>
    /// Drop the newest item (the one being written).
    /// </summary>
    DropWrite
}

/// <summary>
/// Configures the pipeline that delivers captured requests to sinks.
/// </summary>
public sealed class MemoaPipelineOptions
{
    /// <summary>
    /// The pipeline execution mode. Default: <see cref="PipelineMode.Background"/>.
    /// </summary>
    public PipelineMode Mode { get; set; } = PipelineMode.Background;

    /// <summary>
    /// The bounded capacity of the background channel. Default: 1024.
    /// </summary>
    public int ChannelCapacity { get; set; } = 1024;

    /// <summary>
    /// Behavior when the channel is full. Default: <see cref="ChannelFullMode.DropWrite"/>.
    /// </summary>
    public ChannelFullMode FullMode { get; set; } = ChannelFullMode.DropWrite;

    /// <summary>
    /// Number of background workers consuming from the channel. Default: 1.
    /// </summary>
    public int WorkerCount { get; set; } = 1;

    /// <summary>
    /// Maximum time to wait for the background channel to drain on shutdown.
    /// </summary>
    public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
