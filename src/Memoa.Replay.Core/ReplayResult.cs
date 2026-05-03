namespace Memoa.Replay;

/// <summary>
/// Summary of a completed replay session.
/// </summary>
public sealed record ReplayResult(int Total, int Succeeded, int Failed);
