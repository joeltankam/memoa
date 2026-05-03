using Memoa.Replay.Api.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Memoa.Replay.Api;

/// <summary>
/// Extension methods for registering Memoa replay API services.
/// </summary>
public static class MemoaReplayServiceCollectionExtensions
{
    /// <summary>
    /// Adds Memoa replay API services with programmatic configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional action to configure <see cref="MemoaReplayApiOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMemoaReplay(
        this IServiceCollection services,
        Action<MemoaReplayApiOptions>? configure = null)
    {
        var options = new MemoaReplayApiOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<ReplayJobTracker>();
        services.AddHttpClient("MemoaReplay");

        return services;
    }

    /// <summary>
    /// Adds Memoa replay API services with configuration from an <see cref="IConfiguration"/> section.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration section to bind <see cref="MemoaReplayApiOptions"/> from.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMemoaReplay(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = new MemoaReplayApiOptions();
        configuration.Bind(options);

        services.AddSingleton(options);
        services.AddSingleton<ReplayJobTracker>();
        services.AddHttpClient("MemoaReplay");

        return services;
    }
}
