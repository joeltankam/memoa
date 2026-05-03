namespace Memoa;

/// <summary>
/// Root configuration for the Memoa middleware. Bind from the "Memoa" configuration section.
/// </summary>
public sealed class MemoaOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Memoa";

    /// <summary>
    /// Whether request capture is enabled. Default: <c>true</c>.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Configures what is captured from requests and responses.
    /// </summary>
    public MemoaCaptureOptions Capture { get; set; } = new();

    /// <summary>
    /// Configures which requests are captured (path/method filters).
    /// </summary>
    public MemoaFilterOptions Filters { get; set; } = new();

    /// <summary>
    /// Configures the pipeline that delivers captured requests to sinks.
    /// </summary>
    public MemoaPipelineOptions Pipeline { get; set; } = new();

    /// <summary>
    /// The name of the HTTP header to use as correlation identifier.
    /// Default: <c>"X-Correlation-Id"</c>.
    /// </summary>
    public string CorrelationIdHeader { get; set; } = "X-Correlation-Id";
}
