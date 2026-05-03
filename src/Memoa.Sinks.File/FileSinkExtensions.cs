using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Memoa.Sinks.File;

/// <summary>
/// Extension methods for registering the file system sink with Memoa.
/// </summary>
public static class FileSinkExtensions
{
    /// <summary>
    /// Configures Memoa to write captured requests to the local file system.
    /// </summary>
    /// <param name="sinkBuilder">The sink builder.</param>
    /// <param name="outputDirectory">The root output directory.</param>
    /// <param name="configure">Optional action to further configure <see cref="FileSinkOptions"/>.</param>
    /// <returns>The sink builder for chaining.</returns>
    public static MemoaSinkBuilder FileSystem(
        this MemoaSinkBuilder sinkBuilder,
        string outputDirectory,
        Action<FileSinkOptions>? configure = null)
    {
        var options = new FileSinkOptions { OutputDirectory = outputDirectory };
        configure?.Invoke(options);

        sinkBuilder.Services.AddSingleton(options);
        sinkBuilder.Services.AddSingleton<IRequestSink>(sp =>
        {
            return new FileSink(
                sp.GetRequiredService<FileSinkOptions>(),
                sp.GetRequiredService<ILogger<FileSink>>());
        });

        return sinkBuilder;
    }

    /// <summary>
    /// Configures Memoa to write captured requests to the local file system using default directory.
    /// </summary>
    /// <param name="sinkBuilder">The sink builder.</param>
    /// <param name="configure">Optional action to configure <see cref="FileSinkOptions"/>.</param>
    /// <returns>The sink builder for chaining.</returns>
    public static MemoaSinkBuilder FileSystem(
        this MemoaSinkBuilder sinkBuilder,
        Action<FileSinkOptions>? configure = null)
    {
        var options = new FileSinkOptions();
        configure?.Invoke(options);

        sinkBuilder.Services.AddSingleton(options);
        sinkBuilder.Services.AddSingleton<IRequestSink>(sp =>
        {
            return new FileSink(
                sp.GetRequiredService<FileSinkOptions>(),
                sp.GetRequiredService<ILogger<FileSink>>());
        });

        return sinkBuilder;
    }
}
