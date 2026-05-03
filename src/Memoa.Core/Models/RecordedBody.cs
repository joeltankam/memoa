namespace Memoa;

/// <summary>
/// Represents the body of a captured HTTP request or response.
/// </summary>
public sealed record RecordedBody
{
    /// <summary>
    /// The Content-Type header value.
    /// </summary>
    public required string? ContentType { get; init; }

    /// <summary>
    /// The original body length in bytes.
    /// </summary>
    public required long Length { get; init; }

    /// <summary>
    /// The body content as a UTF-8 string, when the content is text-based.
    /// </summary>
    public string? Text { get; init; }

    /// <summary>
    /// The body content as a Base64-encoded string, when the content is binary.
    /// </summary>
    public string? Base64Bytes { get; init; }

    /// <summary>
    /// Indicates whether the body was truncated due to size limits.
    /// </summary>
    public bool Truncated { get; init; }
}
