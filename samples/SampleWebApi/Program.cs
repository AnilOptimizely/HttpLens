using HttpLens.Core.Extensions;
using HttpLens.Dashboard.Extensions;
using Microsoft.AspNetCore.Mvc;
using SampleWebApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Basic setup — no security (suitable for local development).
builder.Services.AddHttpLens();

// ── Security example (commented out) ──────────────────────────────────────────
// Uncomment and tailor to protect the dashboard in non-development environments.
//
// builder.Services.AddHttpLens(builder.Environment, options =>
// {
//     // Bind all options from appsettings.json / appsettings.{Environment}.json
//     builder.Configuration.GetSection("HttpLens").Bind(options);
//
//     // Or configure programmatically:
//     // options.IsEnabled = builder.Environment.IsDevelopment();
//     // options.ApiKey = "my-secret-key";
//     // options.AuthorizationPolicy = "HttpLensAccess";
//     // options.AllowedIpRanges.Add("10.0.0.0/8");
//     // options.AllowedEnvironments.AddRange(["Development", "Staging"]);
// });
// ────────────────────────────────────────────────────────────────────────────────

builder.Services
    .AddHttpClient<GitHubService>(client =>
    {
        client.DefaultRequestHeaders.Add("User-Agent", "HttpLens-Sample/0.1");
    });

builder.Services
    .AddHttpClient<WeatherService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
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

app.Run();
