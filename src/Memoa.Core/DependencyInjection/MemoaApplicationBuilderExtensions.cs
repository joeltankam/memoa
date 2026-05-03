using Memoa.Internal;
using Microsoft.AspNetCore.Builder;

namespace Memoa;

/// <summary>
/// Extension methods for adding Memoa middleware to the ASP.NET Core request pipeline.
/// </summary>
public static class MemoaApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the Memoa request capture middleware to the pipeline.
    /// Should be placed early in the pipeline to capture all requests.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseMemoa(this IApplicationBuilder app)
    {
        return app.UseMiddleware<MemoaMiddleware>();
    }
}
