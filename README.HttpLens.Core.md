# HttpLens.Core

The core interception and storage engine for [HttpLens](https://github.com/AnilOptimizely/HttpLens) — a developer tool that captures every outbound HTTP call your .NET app makes.

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
| **`SqliteTrafficStore`** | Optional SQLite-backed storage for persisted traffic across app restarts |
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

    options.IsEnabled = true;                     // Master switch (default: true)
    options.AllowedEnvironments.AddRange(["Development", "Staging"]); // Env guard
    options.ApiKey = "my-secret-key";             // API key protection
    options.AuthorizationPolicy = "HttpLensAccess"; // ASP.NET Core policy
    options.AllowedIpRanges.AddRange(["127.0.0.1", "10.0.0.0/8"]); // IP allowlist

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
| `IsEnabled` | `true` | Master switch — when `false`, capture stops and dashboard returns 404 |
| `AllowedEnvironments` | `[]` (all) | Only register services in matching environments |
| `ApiKey` | `null` (off) | Require `X-HttpLens-Key` header or `?key=` query param |
| `AuthorizationPolicy` | `null` (off) | Apply a named ASP.NET Core authorization policy |
| `AllowedIpRanges` | `[]` (all) | Restrict by IPv4, IPv6, or CIDR range |
| `ExcludeUrlPatterns` | `[]` | Glob patterns — URLs matching ANY pattern are NOT captured. `*` matches any characters. |
| `IncludeUrlPatterns` | `[]` | Glob patterns — when non-empty, ONLY matching URLs are captured. Exclude takes precedence. |
| `EnableSqlitePersistence` | `false` | Enable persistent SQLite storage instead of in-memory storage |
| `SqliteDatabasePath` | `httplens.db` | SQLite database path used when persistence is enabled |

### URL Filtering

Control which outbound URLs are captured:

```csharp
builder.Services.AddHttpLens(options =>
{
    // Skip health checks and internal calls
    options.ExcludeUrlPatterns.AddRange(["*health*", "https://internal-service/*"]);

    // Or: only capture specific APIs
    options.IncludeUrlPatterns.AddRange(["https://api.github.com/*"]);
});
```

- **Exclude takes precedence** — a URL matching both lists is excluded.
- Empty lists = capture everything (backward compatible).
- Patterns are case-insensitive, `*` matches any sequence of characters.

### Environment-Aware Registration

Use the `AddHttpLens(IHostEnvironment, ...)` overload to skip service registration entirely when the current environment is not in `AllowedEnvironments`. This means zero overhead — no handlers, no storage, no routes:

```csharp
builder.Services.AddHttpLens(builder.Environment, options =>
{
    options.AllowedEnvironments.AddRange(["Development", "Staging"]);
});
```

When the environment is excluded, `ITrafficStore` is not registered. Guard `MapHttpLensDashboard()` accordingly:

```csharp
if (app.Services.GetService<ITrafficStore>() != null)
    app.MapHttpLensDashboard();
```

### Runtime Configuration with `IOptionsMonitor<T>`

All options except `AllowedEnvironments` support runtime reloading via `IOptionsMonitor<T>`. If you set `reloadOnChange: true` on your configuration source, changing `IsEnabled` or `ApiKey` in `appsettings.json` takes effect immediately — no restart required:

```json
// appsettings.json
{
  "HttpLens": {
    "IsEnabled": false,
    "ApiKey": "my-secret-key"
  }
}
```

```csharp
builder.Services.AddHttpLens(options =>
    builder.Configuration.GetSection("HttpLens").Bind(options));
```

> **Note:** `AllowedEnvironments` is evaluated once at registration time. Changing it at runtime has no effect.

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
