# HttpLens.Dashboard

The embedded web dashboard for [HttpLens](https://github.com/AnilOptimizely/HttpLens) — a browser-based UI for inspecting all outbound HTTP traffic captured by `HttpLens.Core`.

> **Most users should install the [`HttpLens`](https://www.nuget.org/packages/HttpLens) meta-package**, which bundles both `HttpLens.Core` and `HttpLens.Dashboard`. Install `HttpLens.Dashboard` directly only if you're building a custom setup.

## Installation

```shell
dotnet add package HttpLens.Dashboard
```

This package depends on `HttpLens.Core` and will pull it in automatically.

## What's Inside

| Component | Description |
|---|---|
| **Embedded SPA** | A dark/light theme single-page application served from embedded resources — no external files needed |
| **Traffic API** | RESTful JSON endpoints for listing, filtering, and exporting captured HTTP traffic |
| **Export endpoints** | One-click export as cURL, C# `HttpClient` code, or HAR 1.2 files |
| **Dashboard middleware** | Serves `index.html`, CSS, and JS bundles directly from the NuGet package |

## Quick Start

```csharp
var builder = WebApplication.CreateBuilder(args);

// 1. Register HttpLens services
builder.Services.AddHttpLens();

// 2. Allow synchronous IO (required for embedded resource serving)
builder.WebHost.ConfigureKestrel(options => options.AllowSynchronousIO = true);

var app = builder.Build();

// 3. Mount the dashboard
app.MapHttpLensDashboard();

app.Run();
```

Then open **https://localhost:5001/_httplens** in your browser.

## Dashboard Features

### 🔍 Traffic Table
- Real-time list of all captured HTTP requests/responses
- Color-coded status codes (2xx green, 4xx orange, 5xx red)
- Sortable by timestamp, method, status, duration
- Search and filter by method, status code, host, or free text

### 📋 Detail Panel
Click any row to see the full details:
- **Request tab** — Method, URI, headers, body (with syntax highlighting for JSON)
- **Response tab** — Status code, headers, body
- **Headers tab** — Combined request + response headers with sensitive values masked
- **Timing tab** — Total duration
- **Correlation tab** — W3C Trace ID, Parent Span ID, inbound request path, HttpClient name
- **Export tab** — Copy as cURL or C# code with one click

### 🔄 Polly Retry Grouping
Retry attempts are grouped visually — the first attempt appears as a normal row, subsequent retries appear indented beneath it with attempt numbers.

### 📦 HAR Export
Download all captured traffic (or filtered results) as a HAR 1.2 file, importable in Chrome DevTools, Firefox, or any HAR viewer.

### 🌙 Dark / Light Theme
Toggle between dark and light themes using the button in the header. Preference is saved to `localStorage`.

## API Endpoints

All endpoints are served under the dashboard base path (default: `/_httplens`):

| Endpoint | Method | Description |
|---|---|---|
| `/_httplens/api/traffic?skip=0&take=100` | `GET` | List traffic records with pagination |
| `/_httplens/api/traffic/{id}` | `GET` | Get a single record by ID |
| `/_httplens/api/traffic` | `DELETE` | Clear all stored records |
| `/_httplens/api/traffic/retrygroup/{groupId}` | `GET` | Get all attempts in a retry group |
| `/_httplens/api/traffic/{id}/export/curl` | `GET` | Export a record as a cURL command |
| `/_httplens/api/traffic/{id}/export/csharp` | `GET` | Export a record as C# HttpClient code |
| `/_httplens/api/traffic/export/har?ids=...` | `GET` | Export records as HAR 1.2 JSON |

## Custom Dashboard Path

```csharp
// Mount at a custom path
app.MapHttpLensDashboard("/my-custom-path");

// Dashboard available at: https://localhost:5001/my-custom-path
// API available at:       https://localhost:5001/my-custom-path/api/traffic
```

## Security

Security is applied **automatically** by `MapHttpLensDashboard()` — no `UseMiddleware` calls are needed in your `Program.cs`. All security layers are opt-in; existing users who don't configure security see zero behavior change.

### Middleware Execution Order

Checks run in this order; earlier layers short-circuit so later ones are skipped:

1. **EnabledGuard** — returns 404 if `IsEnabled = false`
2. **IpAllowlist** — returns 403 if client IP is not in `AllowedIpRanges`
3. **ApiKey** — returns 401 if `X-HttpLens-Key` header or `?key=` query param is missing or wrong
4. **Authorization policy** — evaluated by ASP.NET Core auth middleware

### API Key Authentication

When `ApiKey` is configured, the embedded SPA automatically reads `?key=` from the page URL on load, stores it in `sessionStorage`, and injects it as an `X-HttpLens-Key` header on every API call. If a request returns 401, the UI displays a clear message.

Access the dashboard with the key in the URL:

```
/_httplens?key=my-secret
```

Subsequent navigation within the SPA does not require the query parameter — the key is kept in `sessionStorage` for the duration of the browser session.

### Securing the Dashboard in Production

The recommended pattern for production use:

```csharp
builder.Services.AddHttpLens(builder.Environment, options =>
{
    builder.Configuration.GetSection("HttpLens").Bind(options);
});

// Only map dashboard if services were registered
if (app.Services.GetService<ITrafficStore>() != null)
    app.MapHttpLensDashboard();
```

With `appsettings.Production.json`:

```json
{
  "HttpLens": {
    "IsEnabled": true,
    "ApiKey": "my-production-secret",
    "AllowedIpRanges": ["10.0.0.0/8", "127.0.0.1"]
  }
}
```

### IP Allowlist

Supports exact IPv4 addresses, exact IPv6 addresses, and CIDR notation. IPv4-mapped IPv6 addresses (e.g. `::ffff:127.0.0.1`) are automatically normalised to their IPv4 equivalents before matching.

```csharp
builder.Services.AddHttpLens(options =>
{
    options.AllowedIpRanges.AddRange(["127.0.0.1", "::1", "10.0.0.0/8", "192.168.1.0/24"]);
});
```

### Authorization Policy

Apply any named ASP.NET Core authorization policy to all dashboard and API routes:

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("HttpLensAccess", policy =>
        policy.RequireRole("Admin"));
});

builder.Services.AddHttpLens(options =>
{
    options.AuthorizationPolicy = "HttpLensAccess";
});
```

The policy must be registered in the host application before `MapHttpLensDashboard()` is called.

## Testing with WebApplicationFactory

When using `Microsoft.AspNetCore.Mvc.Testing`, enable synchronous IO on the TestServer:

```csharp
public class MyTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public MyTests(WebApplicationFactory<Program> factory)
    {
        var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.Configure<TestServerOptions>(options =>
                    options.AllowSynchronousIO = true);
            });
        });
        _client = customFactory.CreateClient();
    }

    [Fact]
    public async Task Dashboard_returns_html()
    {
        var response = await _client.GetAsync("/_httplens");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("text/html", response.Content.Headers.ContentType?.ToString());
    }
}
```

## Supported Frameworks

| Framework | Supported |
|---|---|
| .NET 8 | ✅ |
| .NET 9 | ✅ |
| .NET 10 | ✅ |

## License

MIT
