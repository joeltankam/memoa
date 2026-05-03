#if NET7_0_OR_GREATER
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Memoa.Replay.Api;

/// <summary>
/// Endpoint filter that validates the API key header on replay endpoints.
/// Available on .NET 7.0 and later.
/// </summary>
internal sealed class MemoaApiKeyEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var options = context.HttpContext.RequestServices.GetRequiredService<MemoaReplayApiOptions>();

        if (string.IsNullOrEmpty(options.ApiKey))
        {
            return await next(context).ConfigureAwait(false);
        }

        var headerName = options.ApiKeyHeaderName;
        if (!context.HttpContext.Request.Headers.TryGetValue(headerName, out var providedKey) ||
            !string.Equals(providedKey, options.ApiKey, StringComparison.Ordinal))
        {
            return Results.Unauthorized();
        }

        return await next(context).ConfigureAwait(false);
    }
}
#endif
