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

    internal MemoaSinkBuilder(IServiceCollection services)
    {
        Services = services;
    }
}
