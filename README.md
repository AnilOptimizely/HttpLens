# HttpLens

> Install one NuGet package, add two lines of code, and see every outbound HTTP call your app makes — in a browser dashboard.

## Features

- **Automatic interception** — captures all `HttpClient` requests/responses via `IHttpClientFactory`
- **Embedded dashboard** — dark/light theme SPA served at `/_httplens`
- **Sensitive header masking** — Authorization, Cookie, X-Api-Key and custom headers masked before storage
- **Request/response body capture** — with configurable size limits and truncation
- **Polly retry detection** — groups retry attempts visually in the dashboard
- **Export** — one-click copy as cURL or C# `HttpClient` code; download HAR 1.2 files
- **Correlation** — W3C Trace ID, inbound request path, HttpClient name
- **In-memory ring buffer** — configurable max records, thread-safe
- **Real-time updates** — polling fallback (SignalR planned)

## Installation

```shell
dotnet add package HttpLens
```

## Quick Start

```csharp
var builder = WebApplication.CreateBuilder(args);

// 1. Register HttpLens services
builder.Services.AddHttpLens();

var app = builder.Build();

// 2. Mount the dashboard
app.MapHttpLensDashboard();

app.Run();
```

Then open **https://localhost:5001/_httplens** in your browser.

## Configuration

| Option | Default | Description |
|---|---|---|
| `MaxStoredRecords` | `500` | Maximum number of records kept in memory |
| `MaxBodyCaptureSize` | `64000` | Max characters captured per body |
| `DashboardPath` | `/_httplens` | URL path for the dashboard |
| `SensitiveHeaders` | `Authorization`, `Cookie`, `Set-Cookie`, `X-Api-Key` | Headers whose values are masked |
| `CaptureRequestBody` | `true` | Whether to capture request bodies |
| `CaptureResponseBody` | `true` | Whether to capture response bodies |

```csharp
builder.Services.AddHttpLens(options =>
{
    options.MaxStoredRecords = 1000;
    options.SensitiveHeaders.Add("X-Custom-Secret");
    options.CaptureRequestBody = true;
});
```

## Polly Retry Detection

To group Polly retry attempts in the dashboard:

```csharp
builder.Services
    .AddHttpClient("MyClient")
    .AddStandardResilienceHandler()   // Polly resilience
    .Services
    .AddHttpClient("MyClient")
    .AddRetryDetection();             // HttpLens retry tracking
```

Retried requests are grouped visually — the first attempt appears as a normal row, subsequent retries appear indented beneath it.

## Export Features

- **cURL** — Click "📋 Copy" on the Export tab to copy a ready-to-paste cURL command
- **C#** — Copy a complete `HttpClient` / `HttpRequestMessage` code snippet
- **HAR** — Click "📦 HAR" to download all filtered traffic as a HAR 1.2 file (importable in Chrome DevTools)

## Dark / Light Theme

Toggle between dark and light themes using the 🌙/☀️ button in the header. Preference is saved to `localStorage`.

## API Endpoints

| Endpoint | Description |
|---|---|
| `GET /_httplens/api/traffic?skip=0&take=100` | List traffic records |
| `GET /_httplens/api/traffic/{id}` | Get single record |
| `DELETE /_httplens/api/traffic` | Clear all records |
| `GET /_httplens/api/traffic/retrygroup/{groupId}` | Get all attempts in a retry group |
| `GET /_httplens/api/traffic/{id}/export/curl` | Export as cURL |
| `GET /_httplens/api/traffic/{id}/export/csharp` | Export as C# code |
| `GET /_httplens/api/traffic/export/har?ids=...` | Export as HAR 1.2 |

## License

MIT