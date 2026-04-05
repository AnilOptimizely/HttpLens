using HttpLens.Core.Extensions;
using HttpLens.Dashboard.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Register HttpLens services
builder.Services.AddHttpLens();

// Register an HttpClient with Polly resilience and retry detection
builder.Services
    .AddHttpClient("FlakyClient", client =>
    {
        client.BaseAddress = new Uri("https://httpbin.org/");
    })
    .AddStandardResilienceHandler()
    .Services
    .AddHttpClient("FlakyClient")
    .AddRetryDetection();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapHttpLensDashboard();
}

// Endpoint that calls a potentially flaky external service
app.MapGet("/api/flaky", async (IHttpClientFactory factory) =>
{
    var client = factory.CreateClient("FlakyClient");
    try
    {
        var response = await client.GetStringAsync("status/200");
        return Results.Ok(new { status = "ok", body = response });
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: 502);
    }
});

app.Run();
