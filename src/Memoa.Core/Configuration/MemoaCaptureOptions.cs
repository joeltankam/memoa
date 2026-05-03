namespace Memoa;

/// <summary>
/// Configures which parts of HTTP requests and responses are captured.
/// </summary>
public sealed class MemoaCaptureOptions
{
    /// <summary>
    /// Whether to capture request headers. Default: <c>true</c>.
    /// </summary>
    public bool IncludeHeaders { get; set; } = true;

    /// <summary>
    /// When set, only headers matching these patterns are captured.
    /// Supports glob patterns (e.g., "X-*"). If empty, all headers are included (minus deny list).
    /// </summary>
    public List<string> HeaderAllowList { get; set; } = [];

    /// <summary>
    /// Headers matching these patterns are never captured.
    /// Applied after the allow list.
    /// </summary>
    public List<string> HeaderDenyList { get; set; } =
    [
        "Authorization",
        "Cookie",
        "Set-Cookie",
        "Proxy-Authorization"
    ];

    /// <summary>
    /// Whether to capture the query string. Default: <c>true</c>.
    /// </summary>
    public bool IncludeQueryString { get; set; } = true;

    /// <summary>
    /// Whether to capture the request body. Default: <c>true</c>.
    /// </summary>
    public bool IncludeBody { get; set; } = true;

    /// <summary>
    /// Maximum request body size to capture in bytes. Bodies larger than this are truncated.
    /// Default: 1 MB.
    /// </summary>
    public int MaxBodySizeBytes { get; set; } = 1_048_576;

    /// <summary>
    /// Whether to capture the client IP address. Default: <c>false</c>.
    /// </summary>
    public bool IncludeClientIp { get; set; }

    /// <summary>
    /// Whether to capture route values from matched endpoints. Default: <c>true</c>.
    /// </summary>
    public bool IncludeRouteValues { get; set; } = true;

    /// <summary>
    /// Whether to capture the HTTP response. Default: <c>false</c>.
    /// </summary>
    public bool IncludeResponse { get; set; }

    /// <summary>
    /// Whether to capture the response body (only when <see cref="IncludeResponse"/> is true).
    /// Default: <c>false</c>.
    /// </summary>
    public bool IncludeResponseBody { get; set; }

    /// <summary>
    /// Maximum response body size to capture in bytes. Default: 1 MB.
    /// </summary>
    public int MaxResponseBodySizeBytes { get; set; } = 1_048_576;

    /// <summary>
    /// Content-Type patterns that are treated as binary. Bodies with matching types are Base64-encoded.
    /// </summary>
    public List<string> BinaryContentTypePatterns { get; set; } =
    [
        "application/octet-stream",
        "image/*",
        "video/*",
        "audio/*",
        "application/pdf"
    ];
}
