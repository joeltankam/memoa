using Microsoft.Extensions.DependencyInjection;

namespace Memoa;

/// <summary>
/// Builder for configuring Memoa services and sinks.
/// </summary>
public interface IMemoaBuilder
{
    /// <summary>
    /// The service collection being configured.
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// Fluent entry point for registering sinks (e.g., <c>builder.WriteTo.AzureBlobStorage(...)</c>).
    /// </summary>
    MemoaSinkBuilder WriteTo { get; }
}
