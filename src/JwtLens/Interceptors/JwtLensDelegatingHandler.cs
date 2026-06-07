using JwtLens.Analysis;
using JwtLens.Configuration;
using JwtLens.Models;
using JwtLens.Storage;
using Lens.Abstractions;
using Microsoft.Extensions.Options;

namespace JwtLens.Interceptors;

/// <summary>
/// A <see cref="DelegatingHandler"/> that captures JWTs from outbound HTTP requests.
/// </summary>
public sealed class JwtLensDelegatingHandler : DelegatingHandler
{
    private readonly IJwtEventStore _store;
    private readonly IOptionsMonitor<JwtLensOptions> _optionsMonitor;
    private readonly ClaimDiffTracker _diffTracker;
    private readonly IRedactor _redactor;

    /// <summary>
    /// Initializes a new instance of <see cref="JwtLensDelegatingHandler"/>.
    /// </summary>
    public JwtLensDelegatingHandler(
        IJwtEventStore store,
        IOptionsMonitor<JwtLensOptions> optionsMonitor,
        ClaimDiffTracker diffTracker,
        IRedactor redactor)
    {
        _store = store;
        _optionsMonitor = optionsMonitor;
        _diffTracker = diffTracker;
        _redactor = redactor;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue;

        if (options.IsEnabled && options.CaptureOutboundTokens)
        {
            var authHeader = request.Headers.Authorization?.ToString();
            var token = JwtDecoder.ExtractBearerToken(authHeader);

            if (token is not null)
            {
                var captured = JwtEventBuilder.Build(
                    token,
                    TokenDirection.Outbound,
                    request.RequestUri?.ToString(),
                    request.Method.Method,
                    options,
                    _diffTracker,
                    _redactor);

                _store.Add(captured);
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
