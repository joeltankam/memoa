using Memoa.Replay.Api.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Memoa.Replay.Api;

/// <summary>
/// Extension methods for mapping Memoa replay REST API endpoints.
/// </summary>
public static class MemoaReplayEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the Memoa replay REST API endpoints at the configured route prefix.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional action to override <see cref="MemoaReplayApiOptions"/>.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapMemoaReplay(
        this IEndpointRouteBuilder endpoints,
        Action<MemoaReplayApiOptions>? configure = null)
    {
        var options = endpoints.ServiceProvider.GetRequiredService<MemoaReplayApiOptions>();
        configure?.Invoke(options);

        var prefix = options.RoutePrefix.TrimEnd('/');

        var group = endpoints.MapGroup(prefix);

        // Apply authorization policy if configured
        if (!string.IsNullOrEmpty(options.AuthorizationPolicy))
        {
            group.RequireAuthorization(options.AuthorizationPolicy);
        }

#if NET7_0_OR_GREATER
        // Apply API key filter if configured
        if (!string.IsNullOrEmpty(options.ApiKey))
        {
            group.AddEndpointFilter<MemoaApiKeyEndpointFilter>();
        }
#endif

        group.MapGet("/", async (HttpContext context) =>
        {
            var source = context.RequestServices.GetRequiredService<IRequestSource>();
            var query = new RequestQuery
            {
                From = context.Request.Query.ContainsKey("from")
                    ? DateTimeOffset.Parse(context.Request.Query["from"]!)
                    : null,
                To = context.Request.Query.ContainsKey("to")
                    ? DateTimeOffset.Parse(context.Request.Query["to"]!)
                    : null,
                PathPattern = context.Request.Query.ContainsKey("path")
                    ? (string?)context.Request.Query["path"]
                    : null,
                Methods = context.Request.Query.ContainsKey("methods")
                    ? context.Request.Query["methods"].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries)
                    : null,
                Take = context.Request.Query.ContainsKey("take")
                    ? int.Parse(context.Request.Query["take"]!)
                    : null
            };

            var results = new List<RecordedRequest>();
            var take = query.Take ?? 100;

            await foreach (var request in source.ReadAsync(query, context.RequestAborted).ConfigureAwait(false))
            {
                results.Add(request);
                if (results.Count >= take)
                {
                    break;
                }
            }

            return Results.Ok(results);
        }).WithName("MemoaReplay_ListRequests");

        group.MapPost("/run", (ReplayRunRequest request, HttpContext context) =>
        {
            var tracker = context.RequestServices.GetRequiredService<ReplayJobTracker>();
            var jobInfo = tracker.StartJob(request);
            return Results.Accepted($"{prefix}/jobs/{jobInfo.JobId}", jobInfo);
        }).WithName("MemoaReplay_RunReplay");

        group.MapGet("/jobs/{id:guid}", (Guid id, HttpContext context) =>
        {
            var tracker = context.RequestServices.GetRequiredService<ReplayJobTracker>();
            var jobInfo = tracker.GetJob(id);
            return jobInfo is null ? Results.NotFound() : Results.Ok(jobInfo);
        }).WithName("MemoaReplay_GetJob");

        return endpoints;
    }
}
