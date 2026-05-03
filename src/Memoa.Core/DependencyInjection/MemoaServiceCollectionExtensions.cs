using Memoa.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Memoa;

/// <summary>
/// Extension methods for registering Memoa services in the dependency injection container.
/// </summary>
public static class MemoaServiceCollectionExtensions
{
    /// <summary>
    /// Adds Memoa HTTP request capture services with programmatic configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">An action to configure <see cref="MemoaOptions"/>.</param>
    /// <returns>A builder for further Memoa configuration (e.g., registering sinks).</returns>
    public static IMemoaBuilder AddMemoa(this IServiceCollection services, Action<MemoaOptions>? configure = null)
    {
        if (configure is not null)
        {
            services.Configure(configure);
        }

        return AddCoreServices(services, configuration: null);
    }

    /// <summary>
    /// Adds Memoa HTTP request capture services with configuration from an <see cref="IConfiguration"/> section.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration section (typically bound to "Memoa").</param>
    /// <returns>A builder for further Memoa configuration (e.g., registering sinks).</returns>
    public static IMemoaBuilder AddMemoa(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MemoaOptions>(configuration);
        return AddCoreServices(services, configuration);
    }

    private static IMemoaBuilder AddCoreServices(IServiceCollection services, IConfiguration? configuration)
    {
        // Register the pipeline implementations
        services.TryAddSingleton<InlineRequestPipeline>();
        services.TryAddSingleton<BackgroundRequestPipeline>();

        // Register the pipeline factory that chooses based on options
        services.TryAddSingleton<IRequestPipeline>(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MemoaOptions>>().Value;
            return options.Pipeline.Mode switch
            {
                PipelineMode.Inline => sp.GetRequiredService<InlineRequestPipeline>(),
                PipelineMode.Background => sp.GetRequiredService<BackgroundRequestPipeline>(),
                _ => sp.GetRequiredService<BackgroundRequestPipeline>()
            };
        });

        // Register background pipeline as hosted service when in background mode
        services.AddSingleton<IHostedService>(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MemoaOptions>>().Value;
            if (options.Pipeline.Mode == PipelineMode.Background)
            {
                return sp.GetRequiredService<BackgroundRequestPipeline>();
            }

            // Return a no-op hosted service for inline mode
            return new NoOpHostedService();
        });

        return new MemoaBuilder(services, configuration);
    }

    private sealed class NoOpHostedService : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
