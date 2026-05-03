using Microsoft.Extensions.DependencyInjection;

namespace Memoa.Internal;

internal sealed class MemoaBuilder : IMemoaBuilder
{
    public IServiceCollection Services { get; }
    public MemoaSinkBuilder WriteTo { get; }

    public MemoaBuilder(IServiceCollection services)
    {
        Services = services;
        WriteTo = new MemoaSinkBuilder(services);
    }
}
