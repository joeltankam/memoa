namespace Memoa;

/// <summary>
/// Represents a captured HTTP response.
/// </summary>
public sealed record RecordedResponse
{
    /// <summary>
    /// The HTTP status code of the response.
    /// </summary>
    public required int StatusCode { get; init; }

    /// <summary>
    /// The response headers, where each key maps to one or more values.
    /// </summary>
    public IReadOnlyDictionary<string, string[]> Headers { get; init; } = new Dictionary<string, string[]>();

    /// <summary>
    /// The captured response body (if configured).
    /// </summary>
    public RecordedBody? Body { get; init; }

    /// <summary>
    /// The elapsed time in milliseconds from request start to response completion.
    /// </summary>
    public required double ElapsedMs { get; init; }
}
