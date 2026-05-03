using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Memoa.Internal;

internal sealed class MemoaBuilder : IMemoaBuilder
{
    public IServiceCollection Services { get; }
    public IConfiguration? Configuration { get; }
    public MemoaSinkBuilder WriteTo { get; }

    public MemoaBuilder(IServiceCollection services, IConfiguration? configuration = null)
    {
        Services = services;
        Configuration = configuration;
        WriteTo = new MemoaSinkBuilder(services, configuration?.GetSection("Sinks"));
    }
}
