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

#if NET7_0_OR_GREATER
        var group = endpoints.MapGroup(prefix);

        if (!string.IsNullOrEmpty(options.AuthorizationPolicy))
        {
            group.RequireAuthorization(options.AuthorizationPolicy);
        }

        if (!string.IsNullOrEmpty(options.ApiKey))
        {
            group.AddEndpointFilter<MemoaApiKeyEndpointFilter>();
        }

        group.MapGet("/", (Delegate)ListRequestsAsync).WithName("MemoaReplay_ListRequests");
        group.MapPost("/run", (Delegate)RunReplayDelegate).WithName("MemoaReplay_RunReplay");
        group.MapGet("/jobs/{id:guid}", (Delegate)GetJobDelegate).WithName("MemoaReplay_GetJob");
        group.MapPost("/jobs/{id:guid}/cancel", (Delegate)CancelJobDelegate).WithName("MemoaReplay_CancelJob");
#else
        var listBuilder = endpoints.MapGet($"{prefix}", (Delegate)ListRequestsAsync).WithName("MemoaReplay_ListRequests");
        var runBuilder = endpoints.MapPost($"{prefix}/run", (Delegate)RunReplayDelegate).WithName("MemoaReplay_RunReplay");
        var getJobBuilder = endpoints.MapGet($"{prefix}/jobs/{{id:guid}}", (Delegate)GetJobDelegate).WithName("MemoaReplay_GetJob");
        var cancelBuilder = endpoints.MapPost($"{prefix}/jobs/{{id:guid}}/cancel", (Delegate)CancelJobDelegate).WithName("MemoaReplay_CancelJob");

        if (!string.IsNullOrEmpty(options.AuthorizationPolicy))
        {
            listBuilder.RequireAuthorization(options.AuthorizationPolicy);
            runBuilder.RequireAuthorization(options.AuthorizationPolicy);
            getJobBuilder.RequireAuthorization(options.AuthorizationPolicy);
            cancelBuilder.RequireAuthorization(options.AuthorizationPolicy);
        }
#endif

        return endpoints;
    }

    private static async Task<IResult> ListRequestsAsync(HttpContext context)
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
    }

    private static IResult RunReplayDelegate(ReplayRunRequest request, HttpContext context)
    {
        var options = context.RequestServices.GetRequiredService<MemoaReplayApiOptions>();
        var prefix = options.RoutePrefix.TrimEnd('/');
        var tracker = context.RequestServices.GetRequiredService<ReplayJobTracker>();
        var jobInfo = tracker.StartJob(request);
        return Results.Accepted($"{prefix}/jobs/{jobInfo.JobId}", jobInfo);
    }

    private static IResult GetJobDelegate(Guid id, HttpContext context)
    {
        var tracker = context.RequestServices.GetRequiredService<ReplayJobTracker>();
        var jobInfo = tracker.GetJob(id);
        return jobInfo is null ? Results.NotFound() : Results.Ok(jobInfo);
    }

    private static IResult CancelJobDelegate(Guid id, HttpContext context)
    {
        var tracker = context.RequestServices.GetRequiredService<ReplayJobTracker>();
        return tracker.CancelJob(id) ? Results.Ok() : Results.NotFound();
    }
}
