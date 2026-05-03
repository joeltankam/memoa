using Amazon;
using Amazon.S3;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Memoa.Sinks.AmazonS3;

/// <summary>
/// Extension methods for registering the Amazon S3 sink with Memoa.
/// </summary>
public static class AmazonS3SinkExtensions
{
    /// <summary>
    /// Configures Memoa to write captured requests to Amazon S3.
    /// If no <see cref="IAmazonS3"/> service is already registered, a new client is created
    /// using the configured options.
    /// </summary>
    /// <param name="sinkBuilder">The sink builder.</param>
    /// <param name="configure">Action to configure <see cref="AmazonS3SinkOptions"/>.</param>
    /// <returns>The sink builder for chaining.</returns>
    public static MemoaSinkBuilder AmazonS3(
        this MemoaSinkBuilder sinkBuilder,
        Action<AmazonS3SinkOptions> configure)
    {
        var options = new AmazonS3SinkOptions();
        configure(options);

        sinkBuilder.Services.AddSingleton(options);

        // Only register a client if one isn't already registered
        sinkBuilder.Services.TryAddSingleton<IAmazonS3>(_ =>
        {
            var config = new AmazonS3Config();

            if (!string.IsNullOrEmpty(options.ServiceUrl))
            {
                config.ServiceURL = options.ServiceUrl;
                config.ForcePathStyle = options.ForcePathStyle;
            }
            else if (!string.IsNullOrEmpty(options.Region))
            {
                config.RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region);
            }

            return new AmazonS3Client(config);
        });

        sinkBuilder.Services.AddSingleton<IRequestSink>(sp =>
        {
            return new AmazonS3Sink(
                sp.GetRequiredService<IAmazonS3>(),
                sp.GetRequiredService<AmazonS3SinkOptions>(),
                sp.GetRequiredService<ILogger<AmazonS3Sink>>());
        });

        return sinkBuilder;
    }
}
