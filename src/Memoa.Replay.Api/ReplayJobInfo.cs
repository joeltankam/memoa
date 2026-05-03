using Memoa.Replay;

namespace Memoa.Replay.Api;

/// <summary>
/// Information about a running or completed replay job.
/// </summary>
public sealed class ReplayJobInfo
{
    /// <summary>
    /// Unique identifier for the replay job.
    /// </summary>
    public required Guid JobId { get; init; }

    /// <summary>
    /// Current status of the job: Running, Completed, or Failed.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// UTC timestamp when the job was started.
    /// </summary>
    public required DateTimeOffset StartedAtUtc { get; init; }

    /// <summary>
    /// UTC timestamp when the job completed. <c>null</c> while running.
    /// </summary>
    public DateTimeOffset? CompletedAtUtc { get; init; }

    /// <summary>
    /// Replay result summary. <c>null</c> while running.
    /// </summary>
    public ReplayResult? Result { get; init; }
}
