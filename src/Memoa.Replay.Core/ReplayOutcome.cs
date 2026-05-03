namespace Memoa.Replay;

/// <summary>
/// Outcome of replaying a single captured request.
/// </summary>
public sealed record ReplayOutcome(RecordedRequest Request, bool Success, int? StatusCode, string? Error);
