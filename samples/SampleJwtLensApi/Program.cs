using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using JwtLens.Analysis;
using JwtLens.Extensions;
using JwtLens.Storage;
using Lens.Abstractions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddJwtLens();
builder.Services.AddHttpClient("outbound");

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

var app = builder.Build();

app.UseJwtLens();

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

app.MapDelete("/api/jwt/events", (IJwtEventStore store, ClaimDiffTracker diffTracker) =>
{
    store.Clear();
    diffTracker.Clear();
    return Results.Ok(new { message = "cleared" });
});

// Diagnostics endpoint
app.MapGet("/api/jwt/diagnostics", (ILensDiagnosticsContributor contributor) =>
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

app.Run();
