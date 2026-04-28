using HttpLens.Core.Configuration;
using HttpLens.Dashboard.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace HttpLens.Dashboard.Hubs;

/// <summary>
/// SignalR hub for real-time traffic updates.
/// </summary>
/// <param name="optionsMonitor">HttpLens options monitor.</param>
public sealed class TrafficHub(IOptionsMonitor<HttpLensOptions> optionsMonitor) : Hub
{
    /// <inheritdoc />
    public override Task OnConnectedAsync()
    {
        var context = Context.GetHttpContext();
        if (context is null)
            return base.OnConnectedAsync();

        var options = optionsMonitor.CurrentValue;
        if (!options.IsEnabled)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            Context.Abort();
            return Task.CompletedTask;
        }

        var remoteIp = context.Connection.RemoteIpAddress;
        if (options.AllowedIpRanges.Count > 0 &&
            (remoteIp is null || !IpAllowlistMiddleware.IsIpAllowed(remoteIp, options.AllowedIpRanges)))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            Context.Abort();
            return Task.CompletedTask;
        }

        if (!string.IsNullOrEmpty(options.ApiKey))
        {
            var providedKey =
                context.Request.Headers[ApiKeyAuthMiddleware.HeaderName].FirstOrDefault()
                ?? context.Request.Query[ApiKeyAuthMiddleware.QueryParamName].FirstOrDefault();

            if (!string.Equals(providedKey, options.ApiKey, StringComparison.Ordinal))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                Context.Abort();
                return Task.CompletedTask;
            }
        }

        return base.OnConnectedAsync();
    }
}
