using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Memoa.Sinks.Redis;

/// <summary>
/// Extension methods for registering the Redis sink with Memoa.
/// </summary>
public static class RedisSinkExtensions
{
    /// <summary>
    /// Configures Memoa to write captured requests to a Redis Stream.
    /// </summary>
    /// <param name="sinkBuilder">The sink builder.</param>
    /// <param name="connectionString">The Redis connection string.</param>
    /// <param name="configure">Optional action to configure <see cref="RedisSinkOptions"/>.</param>
    /// <returns>The sink builder for chaining.</returns>
    public static MemoaSinkBuilder Redis(
        this MemoaSinkBuilder sinkBuilder,
        string connectionString,
        Action<RedisSinkOptions>? configure = null)
    {
        var options = new RedisSinkOptions { ConnectionString = connectionString };
        configure?.Invoke(options);

        sinkBuilder.Services.AddSingleton(options);
        sinkBuilder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(connectionString));
        sinkBuilder.Services.AddSingleton<IRequestSink>(sp =>
        {
            return new RedisSink(
                sp.GetRequiredService<IConnectionMultiplexer>(),
                sp.GetRequiredService<RedisSinkOptions>(),
                sp.GetRequiredService<ILogger<RedisSink>>());
        });

        return sinkBuilder;
    }

    /// <summary>
    /// Configures Memoa to write captured requests to a Redis Stream using a pre-registered <see cref="IConnectionMultiplexer"/>.
    /// </summary>
    /// <param name="sinkBuilder">The sink builder.</param>
    /// <param name="configure">Optional action to configure <see cref="RedisSinkOptions"/>.</param>
    /// <returns>The sink builder for chaining.</returns>
    public static MemoaSinkBuilder Redis(
        this MemoaSinkBuilder sinkBuilder,
        Action<RedisSinkOptions>? configure = null)
    {
        var options = new RedisSinkOptions();
        configure?.Invoke(options);

        sinkBuilder.Services.AddSingleton(options);
        sinkBuilder.Services.AddSingleton<IRequestSink>(sp =>
        {
            return new RedisSink(
                sp.GetRequiredService<IConnectionMultiplexer>(),
                sp.GetRequiredService<RedisSinkOptions>(),
                sp.GetRequiredService<ILogger<RedisSink>>());
        });

        return sinkBuilder;
    }
}
