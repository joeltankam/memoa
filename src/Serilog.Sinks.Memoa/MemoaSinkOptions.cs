using Serilog.Events;

namespace Serilog.Sinks.Memoa;

/// <summary>
/// Configuration options for the Memoa Serilog sink.
/// </summary>
public sealed class MemoaSinkOptions
{
    /// <summary>
    /// The log event level for captured requests.
    /// Default: <see cref="LogEventLevel.Information"/>.
    /// </summary>
    public LogEventLevel Level { get; set; } = LogEventLevel.Information;

    /// <summary>
    /// Whether to include the request body in the log event.
    /// Default: <c>true</c>.
    /// </summary>
    public bool IncludeRequestBody { get; set; } = true;

    /// <summary>
    /// Whether to include the response body in the log event.
    /// Default: <c>false</c>.
    /// </summary>
    public bool IncludeResponseBody { get; set; }

    /// <summary>
    /// Whether to include request headers in the log event.
    /// Default: <c>true</c>.
    /// </summary>
    public bool IncludeHeaders { get; set; } = true;

    /// <summary>
    /// Maximum body length (in characters) to include in log events.
    /// Bodies exceeding this limit are truncated.
    /// Default: 4096.
    /// </summary>
    public int MaxBodyLength { get; set; } = 4096;
}
