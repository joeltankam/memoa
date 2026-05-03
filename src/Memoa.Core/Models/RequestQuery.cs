namespace Memoa;

/// <summary>
/// Query parameters for reading captured requests from a source.
/// </summary>
public sealed record RequestQuery
{
    /// <summary>
    /// Filter requests captured at or after this UTC time.
    /// </summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>
    /// Filter requests captured at or before this UTC time.
    /// </summary>
    public DateTimeOffset? To { get; init; }

    /// <summary>
    /// Glob pattern to match request paths.
    /// </summary>
    public string? PathPattern { get; init; }

    /// <summary>
    /// Filter by HTTP method(s).
    /// </summary>
    public IReadOnlyCollection<string>? Methods { get; init; }

    /// <summary>
    /// Maximum number of requests to return.
    /// </summary>
    public int? Take { get; init; }
}
