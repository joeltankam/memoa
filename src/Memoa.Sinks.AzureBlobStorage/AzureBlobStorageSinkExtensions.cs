using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Memoa.Sinks.AzureBlobStorage;

/// <summary>
/// Extension methods for registering the Azure Blob Storage sink with Memoa.
/// </summary>
public static class AzureBlobStorageSinkExtensions
{
    /// <summary>
    /// Configures Memoa to write captured requests to Azure Blob Storage.
    /// </summary>
    /// <param name="sinkBuilder">The sink builder.</param>
    /// <param name="connectionString">The Azure Storage connection string.</param>
    /// <param name="configure">Optional action to configure <see cref="AzureBlobStorageSinkOptions"/>.</param>
    /// <returns>The sink builder for chaining.</returns>
    public static MemoaSinkBuilder AzureBlobStorage(
        this MemoaSinkBuilder sinkBuilder,
        string connectionString,
        Action<AzureBlobStorageSinkOptions>? configure = null)
    {
        var options = new AzureBlobStorageSinkOptions { ConnectionString = connectionString };
        configure?.Invoke(options);

        sinkBuilder.Services.AddSingleton(options);
        sinkBuilder.Services.AddSingleton(sp =>
        {
            return new BlobContainerClient(connectionString, options.ContainerName);
        });
        sinkBuilder.Services.AddSingleton<IRequestSink>(sp =>
        {
            return new AzureBlobStorageSink(
                sp.GetRequiredService<BlobContainerClient>(),
                sp.GetRequiredService<AzureBlobStorageSinkOptions>(),
                sp.GetRequiredService<ILogger<AzureBlobStorageSink>>());
        });

        return sinkBuilder;
    }

    /// <summary>
    /// Configures Memoa to write captured requests to Azure Blob Storage
    /// using a pre-registered <see cref="BlobServiceClient"/> (e.g., from <c>AddAzureClients</c>).
    /// </summary>
    /// <param name="sinkBuilder">The sink builder.</param>
    /// <param name="configure">Optional action to configure <see cref="AzureBlobStorageSinkOptions"/>.</param>
    /// <returns>The sink builder for chaining.</returns>
    public static MemoaSinkBuilder AzureBlobStorage(
        this MemoaSinkBuilder sinkBuilder,
        Action<AzureBlobStorageSinkOptions>? configure = null)
    {
        var options = new AzureBlobStorageSinkOptions();
        configure?.Invoke(options);

        sinkBuilder.Services.AddSingleton(options);
        sinkBuilder.Services.AddSingleton(sp =>
        {
            var serviceClient = sp.GetRequiredService<BlobServiceClient>();
            return serviceClient.GetBlobContainerClient(options.ContainerName);
        });
        sinkBuilder.Services.AddSingleton<IRequestSink>(sp =>
        {
            return new AzureBlobStorageSink(
                sp.GetRequiredService<BlobContainerClient>(),
                sp.GetRequiredService<AzureBlobStorageSinkOptions>(),
                sp.GetRequiredService<ILogger<AzureBlobStorageSink>>());
        });

        return sinkBuilder;
    }

    /// <summary>
    /// Configures Memoa to write captured requests to Azure Blob Storage using configuration.
    /// Requires a <c>ConnectionString</c> property in the bound configuration section.
    /// </summary>
    /// <param name="sinkBuilder">The sink builder.</param>
    /// <param name="configuration">The configuration section to bind <see cref="AzureBlobStorageSinkOptions"/> from.</param>
    /// <returns>The sink builder for chaining.</returns>
    public static MemoaSinkBuilder AzureBlobStorage(
        this MemoaSinkBuilder sinkBuilder,
        IConfiguration configuration)
    {
        var options = new AzureBlobStorageSinkOptions();
        configuration.Bind(options);

        sinkBuilder.Services.AddSingleton(options);
        sinkBuilder.Services.AddSingleton(sp =>
        {
            if (!string.IsNullOrEmpty(options.ConnectionString))
            {
                return new BlobContainerClient(options.ConnectionString, options.ContainerName);
            }

            var serviceClient = sp.GetRequiredService<BlobServiceClient>();
            return serviceClient.GetBlobContainerClient(options.ContainerName);
        });
        sinkBuilder.Services.AddSingleton<IRequestSink>(sp =>
        {
            return new AzureBlobStorageSink(
                sp.GetRequiredService<BlobContainerClient>(),
                sp.GetRequiredService<AzureBlobStorageSinkOptions>(),
                sp.GetRequiredService<ILogger<AzureBlobStorageSink>>());
        });

        return sinkBuilder;
    }
}
