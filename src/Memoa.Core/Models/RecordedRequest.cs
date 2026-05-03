namespace Memoa;

/// <summary>
/// Represents a fully captured HTTP request, optionally including the response.
/// </summary>
public sealed record RecordedRequest
{
    /// <summary>
    /// Unique identifier for this captured request.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// UTC timestamp when the request was captured.
    /// </summary>
    public required DateTimeOffset CapturedAtUtc { get; init; }

    /// <summary>
    /// Optional correlation identifier extracted from a configurable header.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// The HTTP method (GET, POST, etc.).
    /// </summary>
    public required string Method { get; init; }

    /// <summary>
    /// The request scheme (http or https).
    /// </summary>
    public required string Scheme { get; init; }

    /// <summary>
    /// The host name of the request.
    /// </summary>
    public required string Host { get; init; }

    /// <summary>
    /// The base path of the request, if any.
    /// </summary>
    public string? PathBase { get; init; }

    /// <summary>
    /// The request path.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// The query string (including the leading '?').
    /// </summary>
    public string? QueryString { get; init; }

    /// <summary>
    /// The HTTP protocol version (e.g., HTTP/1.1, HTTP/2).
    /// </summary>
    public required string Protocol { get; init; }

    /// <summary>
    /// Route values extracted from the matched endpoint.
    /// </summary>
    public IReadOnlyDictionary<string, string?>? RouteValues { get; init; }

    /// <summary>
    /// The captured request headers.
    /// </summary>
    public IReadOnlyDictionary<string, string[]>? Headers { get; init; }

    /// <summary>
    /// The client IP address.
    /// </summary>
    public string? ClientIp { get; init; }

    /// <summary>
    /// The captured request body.
    /// </summary>
    public RecordedBody? Body { get; init; }

    /// <summary>
    /// The captured response, if response capture is enabled.
    /// </summary>
    public RecordedResponse? Response { get; init; }
}
