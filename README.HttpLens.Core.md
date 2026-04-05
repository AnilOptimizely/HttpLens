# HttpLens.Core

The core interception and storage engine for [HttpLens](https://github.com/AnilOptimizely/HttpClientStorybook) — a developer tool that captures every outbound HTTP call your .NET app makes.

> **Most users should install the [`HttpLens`](https://www.nuget.org/packages/HttpLens) meta-package**, which bundles both `HttpLens.Core` and `HttpLens.Dashboard`. Install `HttpLens.Core` directly only if you want the capture engine without the embedded dashboard UI.

## Installation

```shell
dotnet add package HttpLens.Core
```

## What's Inside

| Component | Description |
|---|---|
| **`HttpLensDelegatingHandler`** | A `DelegatingHandler` that automatically intercepts every `HttpClient` request/response registered via `IHttpClientFactory` |
| **`DiagnosticInterceptor`** | Captures traffic from manually-created `HttpClient` instances via `DiagnosticListener` |
| **`InMemoryTrafficStore`** | Thread-safe, ring-buffer storage for captured `HttpTrafficRecord` objects |
| **`HttpLensOptions`** | Configuration for max records, body capture limits, sensitive header masking, and more |
| **`RetryDetectionHandler`** | Groups Polly retry attempts under a shared `RetryGroupId` |
| **Export utilities** | `CurlExporter`, `CSharpExporter`, and `HarExporter` for exporting captured traffic |

## Quick Start

```csharp
var builder = WebApplication.CreateBuilder(args);

// Register HttpLens Core services (interception + storage)
builder.Services.AddHttpLens(options =>
{
    options.MaxStoredRecords = 1000;
    options.MaxBodyCaptureSize = 64_000;
    options.SensitiveHeaders.Add("X-Custom-Secret");
});

// Register your HttpClients as usual — they're automatically intercepted
builder.Services.AddHttpClient("github", client =>
{
    client.BaseAddress = new Uri("https://api.github.com");
    client.DefaultRequestHeaders.Add("User-Agent", "MyApp");
});

var app = builder.Build();
app.Run();
```

## Configuration

```csharp
builder.Services.AddHttpLens(options =>
{
    options.MaxStoredRecords = 500;        // Ring buffer size (default: 500)
    options.MaxBodyCaptureSize = 64_000;   // Max chars per body (default: 64000)
    options.DashboardPath = "/_httplens";  // Base path (default: /_httplens)
    options.CaptureRequestBody = true;     // Capture request bodies (default: true)
    options.CaptureResponseBody = true;    // Capture response bodies (default: true)
    options.EnableDiagnosticInterception = true; // Capture manual HttpClient (default: true)

    // Headers that are masked before storage
    options.SensitiveHeaders.Add("X-Custom-Secret");
});
```

| Option | Default | Description |
|---|---|---|
| `MaxStoredRecords` | `500` | Maximum records kept in the in-memory ring buffer |
| `MaxBodyCaptureSize` | `64000` | Maximum characters captured per request/response body |
| `DashboardPath` | `/_httplens` | URL path prefix for the dashboard and API |
| `SensitiveHeaders` | `Authorization`, `Cookie`, `Set-Cookie`, `X-Api-Key` | Headers whose values are masked with `••••••••` |
| `CaptureRequestBody` | `true` | Whether to capture and store request bodies |
| `CaptureResponseBody` | `true` | Whether to capture and store response bodies |
| `EnableDiagnosticInterception` | `true` | Capture traffic from manually-created `HttpClient` instances |

## Accessing Captured Traffic Programmatically

Inject `ITrafficStore` to access records directly in your code:

```csharp
app.MapGet("/my-traffic", (ITrafficStore store) =>
{
    var records = store.GetAll();
    return Results.Ok(new { total = records.Count, records });
});
```

## Polly Retry Detection

Group retry attempts visually by adding the `RetryDetectionHandler`:

```csharp
builder.Services
    .AddHttpClient("MyClient")
    .AddStandardResilienceHandler()   // Polly resilience
    .Services
    .AddHttpClient("MyClient")
    .AddRetryDetection();             // HttpLens retry tracking
```

## Export Utilities

```csharp
using HttpLens.Core.Export;

// Export a record as a cURL command
string curl = CurlExporter.Export(record);

// Export as C# HttpClient code
string csharp = CSharpExporter.Export(record);

// Export multiple records as HAR 1.2 JSON
string har = HarExporter.Export(records);
```

## Supported Frameworks

| Framework | Supported |
|---|---|
| .NET 8 | ✅ |
| .NET 9 | ✅ |
| .NET 10 | ✅ |

## License

MIT
