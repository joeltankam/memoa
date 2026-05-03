namespace Memoa.Sinks.AmazonS3;

/// <summary>
/// Configuration options for the Amazon S3 sink.
/// </summary>
public sealed class AmazonS3SinkOptions
{
    /// <summary>
    /// The name of the S3 bucket to store captured requests in.
    /// </summary>
    public string BucketName { get; set; } = "memoa-requests";

    /// <summary>
    /// Optional key prefix (virtual directory) for object keys.
    /// Default: <c>null</c>.
    /// </summary>
    public string? KeyPrefix { get; set; }

    /// <summary>
    /// The format for object keys. Supports placeholders:
    /// <c>{year}</c>, <c>{month}</c>, <c>{day}</c>, <c>{hour}</c>, <c>{id}</c>, <c>{method}</c>.
    /// Default: <c>"{year}/{month}/{day}/{hour}/{id}.json"</c>.
    /// </summary>
    public string KeyFormat { get; set; } = "{year}/{month}/{day}/{hour}/{id}.json";

    /// <summary>
    /// The AWS region for the S3 client. When null, the client uses its configured default region.
    /// </summary>
    public string? Region { get; set; }

    /// <summary>
    /// Optional service URL (for S3-compatible endpoints like MinIO or LocalStack).
    /// </summary>
    public string? ServiceUrl { get; set; }

    /// <summary>
    /// Whether to force path-style addressing (required for some S3-compatible services).
    /// </summary>
    public bool ForcePathStyle { get; set; }

    /// <summary>
    /// Whether to create the bucket automatically if it does not exist.
    /// Default: <c>true</c>.
    /// </summary>
    public bool CreateBucketIfNotExists { get; set; } = true;
}
