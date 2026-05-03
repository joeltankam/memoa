namespace Memoa.Sinks.File;

/// <summary>
/// Configuration options for the file system sink.
/// </summary>
public sealed class FileSinkOptions
{
    /// <summary>
    /// The root directory where captured requests are stored.
    /// Default: <c>"./memoa-requests"</c>.
    /// </summary>
    public string OutputDirectory { get; set; } = "./memoa-requests";

    /// <summary>
    /// The format for subdirectory/file names. Supports placeholders:
    /// <c>{year}</c>, <c>{month}</c>, <c>{day}</c>, <c>{hour}</c>, <c>{id}</c>, <c>{method}</c>.
    /// Default: <c>"{year}/{month}/{day}/{hour}/{id}.json"</c>.
    /// </summary>
    public string FileNameFormat { get; set; } = "{year}/{month}/{day}/{hour}/{id}.json";

    /// <summary>
    /// Whether to indent the JSON output. Default: <c>true</c>.
    /// </summary>
    public bool IndentJson { get; set; } = true;
}
