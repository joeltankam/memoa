using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Memoa;

/// <summary>
/// Fluent builder for registering request sinks.
/// Sink extension methods should target this class.
/// </summary>
public sealed class MemoaSinkBuilder
{
    /// <summary>
    /// The service collection being configured.
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// The "Sinks" configuration section, if available.
    /// Sink extensions can use this to bind their options from appsettings.
    /// </summary>
    public IConfiguration? Configuration { get; }

    internal MemoaSinkBuilder(IServiceCollection services, IConfiguration? configuration = null)
    {
        Services = services;
        Configuration = configuration;
    }
}
