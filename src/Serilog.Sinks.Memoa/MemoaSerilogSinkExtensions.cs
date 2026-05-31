using Memoa;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Serilog.Sinks.Memoa;

/// <summary>
/// Extension methods for registering the Memoa Serilog sink.
/// </summary>
public static class MemoaSerilogSinkExtensions
{
    /// <summary>
    /// Writes captured requests to Serilog as structured log events.
    /// </summary>
    /// <param name="builder">The Memoa sink builder.</param>
    /// <param name="logger">The Serilog logger to write events to.</param>
    /// <param name="configure">Optional action to configure <see cref="MemoaSinkOptions"/>.</param>
    /// <returns>The sink builder for chaining.</returns>
    public static MemoaSinkBuilder Serilog(
        this MemoaSinkBuilder builder,
        ILogger logger,
        Action<MemoaSinkOptions>? configure = null)
    {
        var options = new MemoaSinkOptions();
        configure?.Invoke(options);

        builder.Services.AddSingleton<IRequestSink>(new MemoaSerilogSink(logger, options));
        return builder;
    }

    /// <summary>
    /// Writes captured requests to Serilog as structured log events using the static <see cref="Log.Logger"/>.
    /// </summary>
    /// <param name="builder">The Memoa sink builder.</param>
    /// <param name="configure">Optional action to configure <see cref="MemoaSinkOptions"/>.</param>
    /// <returns>The sink builder for chaining.</returns>
    public static MemoaSinkBuilder Serilog(
        this MemoaSinkBuilder builder,
        Action<MemoaSinkOptions>? configure = null)
    {
        var options = new MemoaSinkOptions();
        configure?.Invoke(options);

        builder.Services.AddSingleton<IRequestSink>(sp => new MemoaSerilogSink(Log.Logger, options));
        return builder;
    }
}
