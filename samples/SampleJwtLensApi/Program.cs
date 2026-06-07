using JwtLens.Configuration;
using JwtLens.Extensions;
using JwtLens.Interceptors;
using JwtLens.Storage;
using Lens.Abstractions;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Register JwtLens with environment-aware overload
builder.Services.AddJwtLens(builder.Environment, opts =>
{
    builder.Configuration.GetSection("JwtLens").Bind(opts);
});

// Register a named HttpClient with JwtLensDelegatingHandler for outbound capture
builder.Services.AddHttpClient("OutboundTest")
    .AddHttpMessageHandler<JwtLensDelegatingHandler>();

var app = builder.Build();

// Add JwtLens middleware early in the pipeline
app.UseJwtLens();

// ════════════════════════════════════════════════════════════
// Debug / query endpoints
// ════════════════════════════════════════════════════════════

// GET /api/jwt/events — returns all stored CapturedJwt events
app.MapGet("/api/jwt/events", (IServiceProvider sp) =>
{
    var store = sp.GetService<IJwtEventStore>();
    if (store is null)
        return Results.Ok(Array.Empty<object>());

    return Results.Ok(store.GetAll());
});

// GET /api/jwt/events/count — returns count and totalCaptured
app.MapGet("/api/jwt/events/count", (IServiceProvider sp) =>
{
    var store = sp.GetService<IJwtEventStore>();
    if (store is null)
        return Results.Ok(new { count = 0, totalCaptured = 0L });

    return Results.Ok(new { count = store.Count, totalCaptured = store.TotalCaptured });
});

// DELETE /api/jwt/events — clears the store
app.MapDelete("/api/jwt/events", (IServiceProvider sp) =>
{
    var store = sp.GetService<IJwtEventStore>();
    store?.Clear();
    return Results.Ok(new { message = "Store cleared" });
});

// GET /api/jwt/diagnostics — returns diagnostics snapshot
app.MapGet("/api/jwt/diagnostics", (IServiceProvider sp) =>
{
    var contributor = sp.GetService<ILensDiagnosticsContributor>();
    if (contributor is null)
        return Results.Ok(new { metadata = (object?)null, snapshot = (object?)null });

    return Results.Ok(new
    {
        metadata = contributor.Metadata,
        snapshot = contributor.GetLatestSnapshot()
    });
});

// GET /api/jwt/options — returns current JwtLensOptions
app.MapGet("/api/jwt/options", (IServiceProvider sp) =>
{
    var monitor = sp.GetService<IOptionsMonitor<JwtLensOptions>>();
    if (monitor is null)
        return Results.Ok(new { message = "JwtLens not registered" });

    var opts = monitor.CurrentValue;
    return Results.Ok(new
    {
        opts.IsEnabled,
        warnIfExpiresWithin = opts.WarnIfExpiresWithin.ToString(),
        opts.TrackClaimDiffs,
        opts.FlagWeakAlgorithms,
        opts.MaxStoredEvents,
        opts.CaptureOutboundTokens,
        opts.CaptureInboundTokens,
        sensitiveClaimNames = opts.SensitiveClaimNames.ToList(),
        opts.AllowedEnvironments,
        weakAlgorithms = opts.WeakAlgorithms.ToList()
    });
});

// ════════════════════════════════════════════════════════════
// Trigger endpoints
// ════════════════════════════════════════════════════════════

// GET /api/outbound-test?token={jwt} — triggers outbound HttpClient call with JWT
app.MapGet("/api/outbound-test", async (string? token, IHttpClientFactory httpClientFactory) =>
{
    var client = httpClientFactory.CreateClient("OutboundTest");

    if (!string.IsNullOrEmpty(token))
    {
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    try
    {
        // Make an outbound call to a test endpoint (httpbin or self)
        var response = await client.GetAsync("https://httpbin.org/get");
        var content = await response.Content.ReadAsStringAsync();
        return Results.Ok(new { status = (int)response.StatusCode, outboundCaptured = true });
    }
    catch (HttpRequestException ex)
    {
        // Even if outbound fails, the delegating handler still captures the token
        return Results.Ok(new { status = 0, error = ex.Message, outboundCaptured = true });
    }
});

// GET /api/test — simple endpoint that accepts any request (for inbound token testing)
app.MapGet("/api/test", () => Results.Ok(new { message = "OK" }));

app.Run();
