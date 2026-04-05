using HttpLens.Core.Extensions;
using HttpLens.Dashboard.Extensions;
using Microsoft.AspNetCore.Mvc;
using SampleWebApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpLens();

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
