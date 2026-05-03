namespace Memoa;

/// <summary>
/// Configures request/response filtering for the Memoa middleware.
/// </summary>
public sealed class MemoaFilterOptions
{
    /// <summary>
    /// Glob patterns for paths to include. Default: all paths.
    /// </summary>
    public List<string> PathIncludePatterns { get; set; } = ["/**"];

    /// <summary>
    /// Glob patterns for paths to exclude. Evaluated after include patterns.
    /// </summary>
    public List<string> PathExcludePatterns { get; set; } =
    [
        "/health*",
        "/metrics*",
        "/favicon.ico"
    ];

    /// <summary>
    /// HTTP methods to capture. Default: common methods.
    /// </summary>
    public List<string> Methods { get; set; } =
    [
        "GET",
        "POST",
        "PUT",
        "PATCH",
        "DELETE"
    ];

    /// <summary>
    /// Status code ranges to capture when response recording is enabled (e.g., "200-299", "500").
    /// Empty means all status codes.
    /// </summary>
    public List<string> StatusCodeRanges { get; set; } = [];
}
