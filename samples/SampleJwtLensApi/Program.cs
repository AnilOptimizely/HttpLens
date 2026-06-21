using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using JwtLens.Analysis;
using JwtLens.Configuration;
using JwtLens.Extensions;
using JwtLens.Interceptors;
using JwtLens.Storage;
using Lens.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

var jwtLensSection = builder.Configuration.GetSection("JwtLens");
var jwtLensOptions = jwtLensSection.Get<JwtLensOptions>() ?? new JwtLensOptions();
var isJwtLensAllowed = jwtLensOptions.AllowedEnvironments.Count == 0 ||
    jwtLensOptions.AllowedEnvironments.Contains(builder.Environment.EnvironmentName, StringComparer.OrdinalIgnoreCase);

builder.Services.Configure<JwtLensOptions>(jwtLensSection);
builder.Services.AddJwtLens(builder.Environment, options =>
{
    jwtLensSection.Bind(options);
});
var outboundClientBuilder = builder.Services.AddHttpClient("outbound");
if (isJwtLensAllowed)
{
    outboundClientBuilder.AddHttpMessageHandler<JwtLensDelegatingHandler>();
}

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

var app = builder.Build();

if (isJwtLensAllowed)
{
    app.UseJwtLens();
}

// Basic test endpoint
app.MapGet("/api/test", () => Results.Ok(new { message = "OK", timestamp = DateTimeOffset.UtcNow }));

// Outbound test endpoint - optionally sends an outbound request with a JWT
app.MapGet("/api/outbound-test", async (string? token, IHttpClientFactory clientFactory) =>
{
    var client = clientFactory.CreateClient("outbound");
    var request = new HttpRequestMessage(HttpMethod.Get, "https://httpbin.org/get");

    if (!string.IsNullOrEmpty(token))
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    try
    {
        await client.SendAsync(request);
    }
    catch
    {
        // Ignore connectivity errors — we only care about capturing the outbound token
    }

    return Results.Ok(new { message = "outbound request sent", hadToken = !string.IsNullOrEmpty(token) });
});

// JWT Events API
if (isJwtLensAllowed)
{
    app.MapGet("/api/jwt/events", (IJwtEventStore store) =>
    {
        var events = store.GetAll();
        return Results.Json(events, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        });
    });

    app.MapGet("/api/jwt/events/last", (IJwtEventStore store) =>
    {
        var events = store.GetAll();
        var last = events.Count > 0 ? events[^1] : null;
        if (last == null) return Results.NotFound();
        return Results.Json(last, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        });
    });

    app.MapGet("/api/jwt/events/count", (IJwtEventStore store) =>
    {
        return Results.Ok(new { count = store.Count, totalCaptured = store.TotalCaptured });
    });

    app.MapDelete("/api/jwt/events", ([FromServices] IJwtEventStore store, [FromServices] ClaimDiffTracker diffTracker) =>
    {
        store.Clear();
        diffTracker.Clear();
        return Results.Ok(new { message = "cleared" });
    });

    // Diagnostics endpoint
    app.MapGet("/api/jwt/diagnostics", ([FromServices] ILensDiagnosticsContributor contributor) =>
    {
        var snapshot = contributor.GetLatestSnapshot();
        return Results.Json(new
        {
            metadata = new
            {
                packageId = contributor.Metadata.PackageId,
                displayName = contributor.Metadata.DisplayName,
                version = contributor.Metadata.Version,
                description = contributor.Metadata.Description
            },
            snapshot = snapshot == null ? null : new
            {
                timestamp = snapshot.Timestamp,
                eventCount = snapshot.EventCount,
                data = snapshot.Data
            }
        }, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        });
    });
}
else
{
    app.MapGet("/api/jwt/events", () => Results.Json(Array.Empty<object>(), new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    }));

    app.MapGet("/api/jwt/events/last", () => Results.NotFound());

    app.MapGet("/api/jwt/events/count", () => Results.Ok(new { count = 0, totalCaptured = 0 }));

    app.MapDelete("/api/jwt/events", () => Results.Ok(new { message = "cleared" }));

    app.MapGet("/api/jwt/diagnostics", () => Results.NotFound());
}

app.MapGet("/api/jwt/options", (IOptions<JwtLensOptions> options) =>
{
    return Results.Ok(new
    {
        options.Value.IsEnabled,
        options.Value.CaptureInboundTokens,
        options.Value.CaptureOutboundTokens,
        options.Value.TrackClaimDiffs,
        options.Value.FlagWeakAlgorithms,
        WarnIfExpiresWithin = options.Value.WarnIfExpiresWithin.ToString(),
        options.Value.MaxStoredEvents,
        AllowedEnvironments = options.Value.AllowedEnvironments,
        WeakAlgorithms = options.Value.WeakAlgorithms,
        SensitiveClaimNames = options.Value.SensitiveClaimNames
    });
});

app.Run();
