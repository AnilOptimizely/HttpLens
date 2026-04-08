using HttpLens.Core.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace HttpLens.Dashboard.Middleware;

/// <summary>
/// Middleware that returns 404 for all HttpLens dashboard and API routes when
/// <see cref="HttpLensOptions.IsEnabled"/> is <see langword="false"/>.
/// Requests to other paths are always passed through unchanged.
/// </summary>
internal sealed class EnabledGuardMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _dashboardPath;

    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="dashboardPath">The base URL path of the HttpLens dashboard.</param>
    public EnabledGuardMiddleware(RequestDelegate next, string dashboardPath)
    {
        _next = next;
        _dashboardPath = dashboardPath;
    }

    /// <summary>Processes the request, returning 404 when HttpLens is disabled.</summary>
    public async Task InvokeAsync(HttpContext context, IOptionsMonitor<HttpLensOptions> options)
    {
        if (!options.CurrentValue.IsEnabled &&
            context.Request.Path.StartsWithSegments(_dashboardPath))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await _next(context);
    }
}
