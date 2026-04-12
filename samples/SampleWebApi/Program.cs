using System.Security.Claims;
using System.Text.Encodings.Web;
using HttpLens.Core.Extensions;
using HttpLens.Dashboard.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SampleWebApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Force reloadOnChange explicitly
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

// Test authentication scheme — reads X-Test-User / X-Test-Role headers
builder.Services.AddAuthentication("Test")
    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", null);

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("HttpLensAccess", policy =>
        policy.RequireRole("Admin"));
});

// Read AllowedEnvironments from config at registration time
// (AddHttpLens checks AllowedEnvironments immediately, before DI is built)
var allowedEnvs = builder.Configuration
    .GetSection("HttpLens:AllowedEnvironments")
    .Get<List<string>>() ?? new List<string>();

// Use the environment-aware overload so AllowedEnvironments is checked at registration time
// Bind ALL options from config inside the callback so they're available immediately
builder.Services.AddHttpLens(builder.Environment, opts =>
{
    builder.Configuration.GetSection("HttpLens").Bind(opts);
    opts.AllowedEnvironments = allowedEnvs;
});

builder.Services
    .AddHttpClient<GitHubService>(client =>
    {
        client.DefaultRequestHeaders.Add("User-Agent", "HttpLens-Sample/0.1");
    });

builder.Services
    .AddHttpClient<WeatherService>();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// ═══════════════════════════════════════════════════════════════
// CRITICAL: Only map dashboard if HttpLens services were registered.
// When AllowedEnvironments excludes the current env, AddHttpLens
// skips all registration — ITrafficStore won't be in DI.
// ═══════════════════════════════════════════════════════════════
var httpLensRegistered = app.Services.GetService<HttpLens.Core.Storage.ITrafficStore>() != null;

if (httpLensRegistered)
{
    app.MapHttpLensDashboard();
}

app.MapGet("/api/github", async ([FromServices] GitHubService svc) => Results.Content(await svc.GetUserAsync(), "application/json"));
app.MapGet("/api/weather", async ([FromServices] WeatherService svc) => Results.Content(await svc.GetCurrentWeatherAsync(), "application/json"));

app.MapGet("/api/manual", async () =>
{
    var client = new HttpClient();
    client.DefaultRequestHeaders.Add("User-Agent", "ManualClient/1.0");
    try
    {
        var response = await client.GetStringAsync("https://api.github.com/users/octocat");
        return Results.Content(response, "application/json");
    }
    finally
    {
        client.Dispose();
    }
});

// Debug endpoints — use optional DI to avoid crash when HttpLens is excluded
app.MapGet("/api/debug/store", (IServiceProvider sp) =>
{
    var store = sp.GetService<HttpLens.Core.Storage.ITrafficStore>();
    if (store == null)
        return Results.Ok(new { message = "HttpLens not registered (environment excluded)", recordCount = 0 });

    var records = store.GetAll();
    return Results.Ok(new
    {
        recordCount = records.Count,
        isEmpty = records.Count == 0,
        records = records.Select(r => new
        {
            r.Id,
            r.RequestMethod,
            r.RequestUri,
            r.Timestamp
        })
    });
});

app.MapGet("/api/debug/options", (IConfiguration config, IServiceProvider sp) =>
{
    var monitor = sp.GetService<Microsoft.Extensions.Options.IOptionsMonitor<HttpLens.Core.Configuration.HttpLensOptions>>();
    var opts = monitor?.CurrentValue;
    var registered = sp.GetService<HttpLens.Core.Storage.ITrafficStore>() != null;

    return Results.Ok(new
    {
        rawConfigValue = config["HttpLens:IsEnabled"],
        monitorIsEnabled = opts?.IsEnabled,
        contentRoot = app.Environment.ContentRootPath,
        environment = app.Environment.EnvironmentName,
        apiKey = opts?.ApiKey,
        authorizationPolicy = opts?.AuthorizationPolicy,
        allowedEnvironments = opts?.AllowedEnvironments
            ?? config.GetSection("HttpLens:AllowedEnvironments").Get<List<string>>(),
        maxStoredRecords = opts?.MaxStoredRecords,
        dashboardPath = opts?.DashboardPath,
        httpLensRegistered = registered
    });
});

app.Run();

// ═══════════════════════════════════════════════════════════════
// Test auth handler — reads X-Test-User / X-Test-Role headers
// ═══════════════════════════════════════════════════════════════
public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-User", out var userHeader) ||
            string.IsNullOrEmpty(userHeader.FirstOrDefault()))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim> { new(ClaimTypes.Name, userHeader.First()!) };

        if (Request.Headers.TryGetValue("X-Test-Role", out var roleHeader) &&
            !string.IsNullOrEmpty(roleHeader.FirstOrDefault()))
        {
            claims.Add(new Claim(ClaimTypes.Role, roleHeader.First()!));
        }

        var identity = new ClaimsIdentity(claims, "Test");
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), "Test");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}