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

## Filtering

### URL Exclusion / Inclusion Patterns

Control which outbound HTTP request URLs are captured using glob-style patterns with `*` wildcards:

```csharp
builder.Services.AddHttpLens(options =>
{
    // Skip health checks and internal service calls
    options.ExcludeUrlPatterns.AddRange(["*health*", "https://internal-service/*"]);

    // Only capture calls to specific APIs
    options.IncludeUrlPatterns.AddRange(["https://api.github.com/*", "*/graphql"]);
});
```

| Option | Default | Description |
|---|---|---|
| `ExcludeUrlPatterns` | `[]` | Glob patterns — URLs matching ANY pattern are NOT captured |
| `IncludeUrlPatterns` | `[]` | Glob patterns — when non-empty, ONLY matching URLs are captured |

- **Exclude takes precedence** — a URL matching both lists is excluded.
- Empty lists preserve default behavior (capture everything).
- Patterns are case-insensitive.

### Server-Side Traffic Filtering

The traffic API supports query parameter-based filtering:

```
GET /_httplens/api/traffic?method=GET&status=2&host=github.com&search=repos
```

| Parameter | Match Type | Example | Description |
|---|---|---|---|
| `method` | Exact (case-insensitive) | `?method=GET` | Filter by HTTP method |
| `status` | Prefix | `?status=4` | Matches 400, 404, 429, etc. |
| `host` | Substring (case-insensitive) | `?host=github.com` | Filter by host in URL |
| `search` | Substring (case-insensitive) | `?search=api` | Free-text URL search |

Filters are applied server-side before pagination. The `total` in the response reflects the filtered count.

### Dashboard Filter Bar

The embedded dashboard includes a visual filter bar with:
- **Method dropdown** — filter by GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS
- **Status dropdown** — filter by 2xx, 3xx, 4xx, 5xx
- **Host input** — filter by hostname
- **Search input** — free-text URL search
- **Clear Filters button** — reset all filters

## Security

By default HttpLens applies **no security** — the dashboard is publicly accessible. This preserves the zero-config developer experience. Each security layer is opt-in.

For a comprehensive security guide, see [Security Documentation](docs/SECURITY.md).

### Security Layers

| Layer | Option | Default | Behaviour |
|---|---|---|---|
| **Master switch** | `IsEnabled` | `true` | When `false`, capture stops and dashboard returns 404 |
| **Environment guard** | `AllowedEnvironments` | `[]` (all) | Only register services in matching environments |
| **API key** | `ApiKey` | `null` (off) | Require `X-HttpLens-Key` header or `?key=` query param |
| **IP allowlist** | `AllowedIpRanges` | `[]` (all) | Restrict by IP address or CIDR range |
| **Auth policy** | `AuthorizationPolicy` | `null` (off) | Apply any registered ASP.NET Core auth policy |

### Configuration Examples

**Restrict to development only:**

```csharp
// Automatically skips registration in Production
builder.Services.AddHttpLens(builder.Environment, options =>
{
    options.AllowedEnvironments.AddRange(["Development", "Staging"]);
});
```

**Protect with an API key:**

```csharp
builder.Services.AddHttpLens(options =>
{
    options.ApiKey = "my-secret-key";
});
```

Then access the dashboard at `/_httplens?key=my-secret-key`. The key is stored in `sessionStorage` so subsequent API calls include it automatically via the `X-HttpLens-Key` header.

**Restrict by IP:**

```csharp
builder.Services.AddHttpLens(options =>
{
    options.AllowedIpRanges.AddRange(["127.0.0.1", "10.0.0.0/8", "::1"]);
});
```

**Disable in production via `appsettings.json`:**

`appsettings.Development.json`:
```json
{ "HttpLens": { "IsEnabled": true } }
```

`appsettings.Production.json`:
```json
{ "HttpLens": { "IsEnabled": false } }
```

Then bind in `Program.cs`:

```csharp
builder.Services.AddHttpLens(options =>
    builder.Configuration.GetSection("HttpLens").Bind(options));
```

**Combined example (recommended for shared/staging environments):**

```csharp
builder.Services.AddHttpLens(builder.Environment, options =>
{
    builder.Configuration.GetSection("HttpLens").Bind(options);

    // Override: force-disable in production regardless of config
    if (builder.Environment.IsProduction())
        options.IsEnabled = false;
});
```

### Middleware Order

Security checks are applied automatically inside `MapHttpLensDashboard()` in this order:

1. **EnabledGuard** — returns 404 if `IsEnabled = false`
2. **IpAllowlist** — returns 403 if client IP is not in `AllowedIpRanges`
3. **ApiKey** — returns 401 if `X-HttpLens-Key` / `?key=` is missing or wrong
4. **Authorization policy** — evaluated by ASP.NET Core auth middleware
5. Endpoint handler

No `UseMiddleware` calls are needed in your `Program.cs`.

> **Note:** `MapHttpLensDashboard()` automatically applies all security checks (enabled guard, IP allowlist, API key, and authorization policy) to both the SPA and API routes. If you call `MapHttpLensApi()` directly, only the `authorizationPolicy` parameter (if provided) is applied — IP allowlist and API key checks are skipped.

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
| `GET /_httplens/api/traffic?method=GET&status=2&host=...&search=...` | List with server-side filtering |
| `GET /_httplens/api/traffic/{id}` | Get single record |
| `DELETE /_httplens/api/traffic` | Clear all records |
| `GET /_httplens/api/traffic/retrygroup/{groupId}` | Get all attempts in a retry group |
| `GET /_httplens/api/traffic/{id}/export/curl` | Export as cURL |
| `GET /_httplens/api/traffic/{id}/export/csharp` | Export as C# code |
| `GET /_httplens/api/traffic/export/har?ids=...` | Export as HAR 1.2 |

## License

MIT