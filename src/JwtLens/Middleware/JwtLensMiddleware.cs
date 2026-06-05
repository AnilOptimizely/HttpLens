using JwtLens.Analysis;
using JwtLens.Configuration;
using JwtLens.Interceptors;
using JwtLens.Models;
using JwtLens.Storage;
using Lens.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace JwtLens.Middleware;

/// <summary>
/// ASP.NET Core middleware that inspects inbound requests for JWT ******
/// </summary>
public sealed class JwtLensMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IJwtEventStore _store;
    private readonly IOptionsMonitor<JwtLensOptions> _optionsMonitor;
    private readonly ClaimDiffTracker _diffTracker;
    private readonly IRedactor _redactor;

    /// <summary>
    /// Initializes a new instance of <see cref="JwtLensMiddleware"/>.
    /// </summary>
    public JwtLensMiddleware(
        RequestDelegate next,
        IJwtEventStore store,
        IOptionsMonitor<JwtLensOptions> optionsMonitor,
        ClaimDiffTracker diffTracker,
        IRedactor redactor)
    {
        _next = next;
        _store = store;
        _optionsMonitor = optionsMonitor;
        _diffTracker = diffTracker;
        _redactor = redactor;
    }

    /// <summary>
    /// Processes the HTTP request, extracting and analyzing any JWT ******
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var options = _optionsMonitor.CurrentValue;

        if (options.IsEnabled && options.CaptureInboundTokens)
        {
            var authHeader = context.Request.Headers.Authorization.ToString();
            var token = JwtDecoder.ExtractBearerToken(authHeader);

            if (token is not null)
            {
                var requestUri = $"{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}{context.Request.QueryString}";
                var captured = JwtEventBuilder.Build(
                    token,
                    TokenDirection.Inbound,
                    requestUri,
                    context.Request.Method,
                    options,
                    _diffTracker,
                    _redactor);

                _store.Add(captured);
            }
        }

        await _next(context);
    }
}
