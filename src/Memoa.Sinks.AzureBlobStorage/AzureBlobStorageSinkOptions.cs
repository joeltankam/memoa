namespace Memoa.Sinks.AzureBlobStorage;

/// <summary>
/// Configuration options for the Azure Blob Storage sink.
/// </summary>
public sealed class AzureBlobStorageSinkOptions
{
    /// <summary>
    /// The connection string for the Azure Blob Storage account.
    /// Ignored when <see cref="ServiceUri"/> is set (use DefaultAzureCredential or a named client instead).
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// The URI of the Blob service endpoint. When set, <see cref="ConnectionString"/> is ignored
    /// and authentication falls back to <c>DefaultAzureCredential</c> or a registered <c>BlobServiceClient</c>.
    /// </summary>
    public Uri? ServiceUri { get; set; }

    /// <summary>
    /// The name of the blob container to store captured requests in.
    /// Default: <c>"memoa-requests"</c>.
    /// </summary>
    public string ContainerName { get; set; } = "memoa-requests";

    /// <summary>
    /// Optional prefix (virtual directory) for blob names.
    /// Default: <c>null</c> (no prefix).
    /// </summary>
    public string? BlobPrefix { get; set; }

    /// <summary>
    /// Whether to create the container automatically if it does not exist.
    /// Default: <c>true</c>.
    /// </summary>
    public bool CreateContainerIfNotExists { get; set; } = true;

    /// <summary>
    /// The format of the blob name. Supports placeholders:
    /// <c>{year}</c>, <c>{month}</c>, <c>{day}</c>, <c>{hour}</c>, <c>{id}</c>, <c>{method}</c>.
    /// Default: <c>"{year}/{month}/{day}/{hour}/{id}.json"</c>.
    /// </summary>
    public string BlobNameFormat { get; set; } = "{year}/{month}/{day}/{hour}/{id}.json";
}
