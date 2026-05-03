namespace Memoa.Sinks.Redis;

/// <summary>
/// Configuration options for the Redis sink.
/// </summary>
public sealed class RedisSinkOptions
{
    /// <summary>
    /// Redis connection string (e.g., "localhost:6379").
    /// Ignored when a pre-registered <c>IConnectionMultiplexer</c> is used.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// The Redis Stream key where captured requests are appended.
    /// Default: <c>"memoa:requests"</c>.
    /// </summary>
    public string StreamKey { get; set; } = "memoa:requests";

    /// <summary>
    /// Maximum number of entries in the stream (MAXLEN). When exceeded, oldest entries are trimmed.
    /// Default: <c>10000</c>. Set to <c>null</c> for unlimited.
    /// </summary>
    public int? MaxLength { get; set; } = 10_000;

    /// <summary>
    /// The Redis database index to use.
    /// Default: <c>-1</c> (default database).
    /// </summary>
    public int Database { get; set; } = -1;

    /// <summary>
    /// Optional key prefix for the stream key.
    /// Default: <c>null</c>.
    /// </summary>
    public string? KeyPrefix { get; set; }
}
