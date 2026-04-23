using HttpLens.Core.Configuration;
using HttpLens.Dashboard.Api;
using HttpLens.Dashboard.Hubs;
using HttpLens.Dashboard.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HttpLens.Dashboard.Extensions;

/// <summary>Extension methods for mounting the HttpLens dashboard onto a route builder.</summary>
public static class EndpointRouteBuilderExtensions
{
    /// <summary>
    /// Mounts the HttpLens dashboard SPA and its JSON API at <paramref name="path"/>.
    /// Security middleware (<see cref="EnabledGuardMiddleware"/>, <see cref="IpAllowlistMiddleware"/>,
    /// <see cref="ApiKeyAuthMiddleware"/>) is applied automatically — no additional
    /// <c>UseMiddleware</c> calls are required in the host application.
    /// </summary>
    /// <param name="endpoints">The application's endpoint route builder.</param>
    /// <param name="path">The base URL path. Defaults to <c>/_httplens</c>.</param>
    public static IEndpointRouteBuilder MapHttpLensDashboard(
        this IEndpointRouteBuilder endpoints,
        string path = "/_httplens")
    {
        // Resolve the authorization policy from current options.
        var optionsMonitor = endpoints.ServiceProvider.GetRequiredService<IOptionsMonitor<HttpLensOptions>>();
        var authorizationPolicy = optionsMonitor.CurrentValue.AuthorizationPolicy;

        // Register the JSON API endpoints with the optional authorization policy,
        // then apply a security endpoint filter for EnabledGuard / IpAllowlist / ApiKey.
        var apiGroup = endpoints.MapHttpLensApi(path, authorizationPolicy);
        apiGroup.AddEndpointFilter(async (ctx, next) =>
        {
            var securityFailure = ValidateSecurity(ctx.HttpContext);
            if (securityFailure is not null)
                return securityFailure;

            return await next(ctx);
        });

        // SignalR endpoint for real-time updates (when SignalR services are available).
        if (endpoints.ServiceProvider.GetService<Microsoft.AspNetCore.SignalR.HubLifetimeManager<TrafficHub>>() is not null)
        {
            var hubEndpoint = endpoints.MapHub<TrafficHub>($"{path}/hub").ExcludeFromDescription();
            ApplySecurityGate(hubEndpoint);
            if (!string.IsNullOrEmpty(authorizationPolicy))
                hubEndpoint.RequireAuthorization(authorizationPolicy);
        }

        // Build a security sub-pipeline that wraps each SPA route.
        // Order: EnabledGuard → IpAllowlist → ApiKey → handler.
        RequestDelegate BuildSecuredHandler(RequestDelegate handler)
        {
            var appBuilder = endpoints.CreateApplicationBuilder();
            appBuilder.UseMiddleware<EnabledGuardMiddleware>(path);
            appBuilder.UseMiddleware<IpAllowlistMiddleware>(path);
            appBuilder.UseMiddleware<ApiKeyAuthMiddleware>(path);
            appBuilder.Run(handler);
            return appBuilder.Build();
        }

        // Shared handler that serves static assets or falls back to index.html.
        async Task ServeStaticOrIndex(HttpContext context)
        {
            var slug = (string?)context.Request.RouteValues["slug"] ?? string.Empty;

            // Empty slug means /_httplens or /_httplens/ — serve index.html.
            if (string.IsNullOrEmpty(slug))
            {
                context.Response.ContentType = "text/html; charset=utf-8";
                await DashboardMiddleware.TryServeResourceAsync(DashboardMiddleware.IndexHtmlResourceName, context);
                return;
            }

            var resourceName = BuildResourceName(slug);

            if (!await DashboardMiddleware.TryServeResourceAsync(resourceName, context))
            {
                // SPA fallback — serve index.html for unknown paths.
                context.Response.ContentType = "text/html; charset=utf-8";
                await DashboardMiddleware.TryServeResourceAsync(DashboardMiddleware.IndexHtmlResourceName, context);
            }
        }

        var securedPipeline = BuildSecuredHandler(ServeStaticOrIndex);

        // Map both /_httplens/{**slug} (catches /_httplens/ and all sub-paths)
        var catchAll = endpoints.Map($"{path}/{{**slug}}", securedPipeline).ExcludeFromDescription();

        // Map /_httplens exactly (no trailing slash) using the same handler — no redirect.
        var baseRoute = endpoints.Map(path, securedPipeline).ExcludeFromDescription();

        // Apply ASP.NET Core authorization policy to SPA routes when configured.
        if (!string.IsNullOrEmpty(authorizationPolicy))
        {
            catchAll.RequireAuthorization(authorizationPolicy);
            baseRoute.RequireAuthorization(authorizationPolicy);
        }

        return endpoints;
    }

    private static IResult? ValidateSecurity(HttpContext context)
    {
        var monitor = context.RequestServices.GetRequiredService<IOptionsMonitor<HttpLensOptions>>();
        var opts = monitor.CurrentValue;

        if (!opts.IsEnabled)
            return Results.NotFound();

        var remoteIp = context.Connection.RemoteIpAddress;
        if (opts.AllowedIpRanges.Count > 0 &&
            (remoteIp is null || !IpAllowlistMiddleware.IsIpAllowed(remoteIp, opts.AllowedIpRanges)))
        {
            return Results.Json(
                new { error = "Access denied" },
                statusCode: StatusCodes.Status403Forbidden);
        }

        var apiKey = opts.ApiKey;
        if (string.IsNullOrEmpty(apiKey))
            return null;

        var providedKey =
            context.Request.Headers[ApiKeyAuthMiddleware.HeaderName].FirstOrDefault()
            ?? context.Request.Query[ApiKeyAuthMiddleware.QueryParamName].FirstOrDefault();

        return string.Equals(providedKey, apiKey, StringComparison.Ordinal)
            ? null
            : Results.Json(
                new { error = "Invalid or missing HttpLens API key" },
                statusCode: StatusCodes.Status401Unauthorized);
    }

    private static void ApplySecurityGate(IEndpointConventionBuilder builder)
    {
        builder.Add(endpointBuilder =>
        {
            var original = endpointBuilder.RequestDelegate;
            if (original is null)
                return;

            endpointBuilder.RequestDelegate = async context =>
            {
                var securityFailure = ValidateSecurity(context);
                if (securityFailure is not null)
                {
                    await securityFailure.ExecuteAsync(context);
                    return;
                }

                await original(context);
            };
        });
    }

    private static string BuildResourceName(string slug)
    {
        // Convert URL path segments to embedded resource name convention.
        // e.g. "js/httplens.bundle.js" → "HttpLens.Dashboard.wwwroot.js.httplens.bundle.js"
        var normalized = slug.Replace('/', '.').TrimStart('.');
        return $"HttpLens.Dashboard.wwwroot.{normalized}";
    }
}
