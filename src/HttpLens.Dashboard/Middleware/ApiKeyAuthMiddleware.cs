using HttpLens.Core.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace HttpLens.Dashboard.Middleware;

/// <summary>
/// Middleware that protects all HttpLens routes with an API key.
/// The key can be provided via the <c>X-HttpLens-Key</c> header or <c>?key=</c> query parameter.
/// When no API key is configured in options, all requests pass through.
/// </summary>
internal sealed class ApiKeyAuthMiddleware
{
    /// <summary>Name of the request header used to supply the API key.</summary>
    internal const string HeaderName = "X-HttpLens-Key";

    /// <summary>Name of the query parameter used to supply the API key.</summary>
    internal const string QueryParamName = "key";

    private readonly RequestDelegate _next;
    private readonly string _dashboardPath;

    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="dashboardPath">The base URL path of the HttpLens dashboard.</param>
    public ApiKeyAuthMiddleware(RequestDelegate next, string dashboardPath)
    {
        _next = next;
        _dashboardPath = dashboardPath;
    }

    /// <summary>Processes the request, enforcing API key authentication for HttpLens routes.</summary>
    public async Task InvokeAsync(HttpContext context, IOptionsMonitor<HttpLensOptions> options)
    {
        var apiKey = options.CurrentValue.ApiKey;

        // No API key configured — allow all requests (dev mode).
        if (string.IsNullOrEmpty(apiKey))
        {
            await _next(context);
            return;
        }

        // Only protect HttpLens routes.
        if (!context.Request.Path.StartsWithSegments(_dashboardPath))
        {
            await _next(context);
            return;
        }

        // Check header first, then query parameter.
        var providedKey = context.Request.Headers[HeaderName].FirstOrDefault()
                       ?? context.Request.Query[QueryParamName].FirstOrDefault();

        if (!string.Equals(providedKey, apiKey, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("""{"error":"Invalid or missing HttpLens API key"}""");
            return;
        }

        await _next(context);
    }
}
