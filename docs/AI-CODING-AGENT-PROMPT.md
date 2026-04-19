# Prompt for AI Coding Agent: Build HttpLens v1.3 + v1.5 Features

> **Combined prompt covering both the v1.3 "Real-Time & Persistence" milestone and the v1.5 "Advanced Protocol & Visualization" milestone.**

---

## Repository & Architecture Context

You are working on the **HttpLens** repository — a .NET NuGet package that intercepts outbound `HttpClient` traffic and displays it in an embedded browser dashboard. The repo is at `/home/runner/work/HttpLens/HttpLens`.

### Solution Structure

```
HttpLens.slnx
├── src/
│   ├── HttpLens.Core/            ← Core interception, storage, models, export, filtering
│   ├── HttpLens.Dashboard/       ← ASP.NET Core endpoints, middleware, embedded SPA
│   └── HttpLens/                 ← Meta-package referencing Core + Dashboard
├── tests/
│   ├── HttpLens.Core.Tests/
│   ├── HttpLens.Dashboard.Tests/
│   └── HttpLens.EndToEnd.Tests/
├── samples/
│   ├── SampleWebApi/
│   └── SampleWithPolly/
├── docs/
│   └── SECURITY.md
├── Directory.Build.props         ← Shared build properties (LangVersion=latest, Nullable=enable, net8.0/net9.0/net10.0)
├── Directory.Packages.props      ← Central package management
├── CHANGELOG.md
├── README.md
├── README.HttpLens.Core.md
└── README.HttpLens.Dashboard.md
```

### Current Version: 1.2.0

The changelog documents the complete evolution: 0.1.0 → 0.5.0 → 1.0.0 → 1.1.0 → 1.2.0 → 2.0.0 (security). Current packages target `net8.0;net9.0;net10.0`.

---

## Existing Codebase — Complete File Inventory

### HttpLens.Core

#### Models

- **`HttpTrafficRecord`** (`Models/HttpTrafficRecord.cs`) — The central data model. Sealed class with:
  - `Guid Id` (init, auto-generated)
  - `DateTimeOffset Timestamp`, `TimeSpan Duration`
  - `string HttpClientName` (default: `"(unnamed)"`)
  - Request: `RequestMethod`, `RequestUri`, `RequestHeaders` (`Dictionary<string, string[]>`), `RequestBody` (nullable), `RequestContentType`, `RequestBodySizeBytes`
  - Response: `ResponseStatusCode` (nullable int), `ResponseHeaders`, `ResponseBody`, `ResponseContentType`, `ResponseBodySizeBytes`
  - Outcome: `bool IsSuccess`, `string? Exception`
  - Correlation: `string? TraceId`, `string? ParentSpanId`, `string? InboundRequestPath`
  - Retry: `int AttemptNumber` (default 1), `Guid? RetryGroupId`
  - HAR models: `HarRoot`, `HarLog`, `HarEntry`, `HarRequest`, `HarResponse`, `HarContent`, `HarPostData`, `HarTimings`, `HarCache`, `HarCreator`, `HarNameValue`
  - `RetryGroup` model

#### Configuration

- **`HttpLensOptions`** (`Configuration/HttpLensOptions.cs`) — All options:
  - `int MaxStoredRecords` (default 500)
  - `int MaxBodyCaptureSize` (default 64,000)
  - `string DashboardPath` (default `"/_httplens"`)
  - `HashSet<string> SensitiveHeaders` (Authorization, Cookie, Set-Cookie, X-Api-Key)
  - `bool CaptureRequestBody` / `CaptureResponseBody` (both default true)
  - `bool EnableDiagnosticInterception` (default true)
  - `bool IsEnabled` (master switch, default true)
  - `List<string> AllowedEnvironments` (empty = all)
  - `string? ApiKey`, `string? AuthorizationPolicy`
  - `List<string> AllowedIpRanges` (IPv4/IPv6/CIDR)
  - `List<string> ExcludeUrlPatterns` / `IncludeUrlPatterns` (glob with `*` wildcard)

#### Storage

- **`ITrafficStore`** (`Storage/ITrafficStore.cs`) — Interface:
  - `event Action<HttpTrafficRecord>? OnRecordAdded`
  - `int Count`
  - `void Add(HttpTrafficRecord record)`
  - `IReadOnlyList<HttpTrafficRecord> GetAll()`
  - `HttpTrafficRecord? GetById(Guid id)`
  - `IReadOnlyList<HttpTrafficRecord> GetByRetryGroupId(Guid groupId)`
  - `void Clear()`

- **`InMemoryTrafficStore`** (`Storage/InMemoryTrafficStore.cs`) — Thread-safe `ConcurrentQueue<T>` ring-buffer. Uses `IOptions<HttpLensOptions>` for `MaxStoredRecords`. Fires `OnRecordAdded` after each `Add()`.

#### Interceptors

- **`HttpLensDelegatingHandler`** (`Interceptors/HttpLensDelegatingHandler.cs`) — Primary capture path for `IHttpClientFactory` clients. Uses `IOptionsMonitor<HttpLensOptions>`. Checks master switch, URL patterns, captures request/response/body, reads retry context from `HttpRequestMessage.Options`, masks sensitive headers, stores via `ITrafficStore.Add()`. Sets `HttpLens.CapturedByHandler` deduplication flag.

- **`DiagnosticInterceptor`** (`Interceptors/DiagnosticInterceptor.cs`) — Process-wide capture via `System.Diagnostics.DiagnosticListener` for manually-newed `HttpClient` instances. Implements `IObserver<DiagnosticListener>` and `IObserver<KeyValuePair<string, object?>>`. Handles `HttpRequestOut.Start`, `HttpRequestOut.Stop`, `Exception` events. Bodies NOT available via DiagnosticListener. Uses deduplication flag. Sets `HttpClientName = "(manual)"`.

- **`DiagnosticInterceptorHostedService`** — `IHostedService` managing interceptor lifecycle.

- **`RetryDetectionHandler`** (`Interceptors/RetryDetectionHandler.cs`) — `DelegatingHandler` that tags requests with `HttpLens.RetryGroupId` (Guid) and `HttpLens.AttemptNumber` (int) via `HttpRequestMessage.Options`.

- **`BodyCapture`** (`Interceptors/BodyCapture.cs`) — Static helper `CaptureAsync(HttpContent?, int maxSize, CancellationToken)` → `(string? Body, long? SizeBytes)`. Buffers content via `LoadIntoBufferAsync()`, reads as string, truncates at limit.

- **`HeaderSnapshot`** (`Interceptors/HeaderSnapshot.cs`) — Merges `HttpHeaders` collections into `Dictionary<string, string[]>`.

- **`SensitiveHeaderMasker`** (`Interceptors/SensitiveHeaderMasker.cs`) — Masks header values: short values → `"••••••••"`, long values → first 4 + `"••••••••"` + last 4.

- **`UrlPatternMatcher`** (`Interceptors/UrlPatternMatcher.cs`) — Glob pattern matching for exclude/include URL filtering. `*` → `.*` regex conversion.

#### Filtering

- **`TrafficFilterCriteria`** (`Filtering/TrafficFilterCriteria.cs`) — Immutable record: `Method`, `Status` (prefix match), `Host` (substring), `Search` (case-insensitive substring). `IsEmpty` property.

- **`TrafficFilter`** (`Filtering/TrafficFilter.cs`) — Static `Apply()` method. AND logic across all non-empty criteria.

#### Export

- **`CurlExporter`** — Generates `curl -X METHOD 'url' -H '...' -d '...'` commands.
- **`CSharpExporter`** — Generates `HttpClient` / `HttpRequestMessage` C# code.
- **`HarExporter`** — Generates HAR 1.2 JSON from `IReadOnlyList<HttpTrafficRecord>`.

#### Extensions

- **`ServiceCollectionExtensions.AddHttpLens()`** — Registers `ITrafficStore` (singleton), `HttpLensDelegatingHandler` (transient), auto-attaches to all `IHttpClientFactory` clients via `ConfigureAll<HttpClientFactoryOptions>`, registers `DiagnosticInterceptor` + hosted service. Overload with `IHostEnvironment` for environment checks.

- **`HttpClientBuilderExtensions`** — `AddHttpLensHandler()` and `AddRetryDetection()` for per-client opt-in.

### HttpLens.Dashboard

#### API

- **`TrafficApiEndpoints.MapHttpLensApi()`** (`Api/TrafficApiEndpoints.cs`) — Minimal API endpoints:
  - `GET /api/traffic?skip=0&take=100&method=&status=&host=&search=` → `{ total, records }`
  - `GET /api/traffic/{id:guid}` → single record or 404
  - `DELETE /api/traffic` → clear all
  - `GET /api/traffic/retrygroup/{groupId:guid}` → records by retry group
  - `GET /api/traffic/{id:guid}/export/curl` → text/plain
  - `GET /api/traffic/{id:guid}/export/csharp` → text/plain
  - `GET /api/traffic/export/har?ids=` → HAR JSON
  - All endpoints use `.ExcludeFromDescription()`
  - Returns `RouteGroupBuilder` for customization

#### Extensions

- **`EndpointRouteBuilderExtensions.MapHttpLensDashboard()`** — Mounts SPA + API at configurable path. Builds security pipeline: `EnabledGuardMiddleware` → `IpAllowlistMiddleware` → `ApiKeyAuthMiddleware`. Endpoint filter on API group checks master switch, IP allowlist, API key. SPA fallback to `index.html`.

#### Middleware

- **`EnabledGuardMiddleware`** — Returns 404 when `IsEnabled = false`
- **`IpAllowlistMiddleware`** — CIDR matching with IPv4-mapped IPv6 normalization
- **`ApiKeyAuthMiddleware`** — `X-HttpLens-Key` header or `?key=` query param; 401 JSON response
- **`DashboardMiddleware`** — Serves embedded resources from assembly

### TypeScript SPA (`dashboard-ui/src/`)

#### Types (`types/traffic.ts`)

```typescript
interface HttpTrafficRecord {
  id, timestamp, duration, httpClientName,
  requestMethod, requestUri, requestHeaders, requestBody, requestContentType, requestBodySizeBytes,
  responseStatusCode, responseHeaders, responseBody, responseContentType, responseBodySizeBytes,
  isSuccess, exception,
  traceId, parentSpanId, inboundRequestPath,
  attemptNumber, retryGroupId
}
interface TrafficListResponse { total: number; records: HttpTrafficRecord[] }
type StatusClass = 'success' | 'redirect' | 'client-error' | 'server-error' | 'error'
type DetailTab = 'request' | 'response' | 'headers' | 'timing' | 'correlation' | 'export'
interface FilterState { method, status, host, search }
type ConnectionStatus = 'connected' | 'reconnecting' | 'disconnected'
```

#### State (`state/store.ts`)

- `AppState`: `records`, `selectedId`, `filters`, `connectionStatus`, `totalServerRecords`
- `Store` class: `subscribe()`, `notify()`, `setRecords()`, `prependRecord()`, `selectRecord()`, `setFilters()`, `setConnectionStatus()`, `clearRecords()`, `getSelectedRecord()`, `getFilteredRecords()`, `getState()`

#### Services

- **`TrafficApiService`** (`services/traffic-api.service.ts`) — `fetchTraffic()`, `fetchById()`, `clearAll()`. API key injection via `X-HttpLens-Key` header (stored in `sessionStorage`, captured from `?key=` URL param).

- **`PollingService`** (`services/polling.service.ts`) — `setInterval` at 2-second intervals calling `fetchTraffic()`, updates store. Sets `connectionStatus`.

#### Components

- **`TrafficTable`** (`components/traffic-table.ts`) — Renders traffic rows with retry grouping, method badges, status badges, inbound badges. Event delegation for row click → `store.selectRecord()`.

- **`DetailPanel`** (`components/detail-panel.ts`) — Tabs: request, response, headers, timing, correlation, export. Renders headers tables, JSON-formatted bodies, timing info, correlation data, cURL/C#/HAR exports with clipboard copy.

- **`FilterBar`** (`components/filter-bar.ts`) — Method dropdown, status dropdown, host input, search input, clear button. Wired to `store.setFilters()`.

- **`Exporters`** (`components/exporters.ts`) — Clipboard copy event delegation, client-side HAR generation, download as `.har` file.

#### Utilities

- **`formatters.ts`** — `getStatusClass()`, `formatTime()`, `parseDurationMs()`, `formatDuration()`, `formatSize()`, `truncateUrl()`, `extractHost()`
- **`html.ts`** — `escapeHtml()`, `prettyPrintJson()`
- **`dom.ts`** — `$()`, `$$()`, `setHtml()`, `toggleClass()`, `createElement()`

#### Entry Point (`index.ts`)

- `bootstrap()` — Creates services, initializes components, wires buttons (clear, refresh, HAR export, single HAR), theme toggle. Fetches initial data, starts polling.

### Build & Package Management

- **`Directory.Build.props`** — `LangVersion=latest`, `Nullable=enable`, `ImplicitUsings=enable`, `TreatWarningsAsErrors=false`, Version 1.0.0
- **`Directory.Packages.props`** — Central package management:
  - `Microsoft.Extensions.Http` 8.0.0
  - `Microsoft.Extensions.Http.Resilience` 9.0.0
  - `Microsoft.Extensions.Hosting.Abstractions` 8.0.0
  - `Microsoft.Extensions.Options` 8.0.0
  - `Microsoft.AspNetCore.TestHost` 9.0.0
  - `Microsoft.Extensions.DependencyInjection` 8.0.0
  - `xunit` 2.9.0, `xunit.runner.visualstudio` 2.8.2
  - `Microsoft.NET.Test.Sdk` 17.11.1
  - `coverlet.collector` 6.0.2
  - `Moq` 4.20.72

### Coding Conventions (MUST follow)

- File-scoped namespaces (`namespace X;`)
- Primary constructors on classes
- `sealed` on all non-abstract classes
- XML doc comments on all public APIs
- `IOptionsMonitor<HttpLensOptions>` for runtime config (not `IOptions<T>`)
- Deduplication via `HttpRequestMessage.Options` flags
- All dashboard API endpoints use `.ExcludeFromDescription()`
- Security applied via endpoint filters and middleware pipeline in `MapHttpLensDashboard()`
- Central package management in `Directory.Packages.props` — all new NuGet references go here

---

## v1.3 Features — "Real-Time & Persistence" (4–6 weeks)

> Theme: SignalR live push, SQLite persistence, real-time dashboard

### FEATURE 1: SignalR Real-Time Push

Replace 2-second polling with SignalR WebSocket push for instant traffic updates.

#### Backend

1. **Add SignalR package** to `Directory.Packages.props`:
   - `Microsoft.AspNetCore.SignalR` (appropriate version for net8.0+)

2. **Create `TrafficHub`** (`HttpLens.Dashboard/Hubs/TrafficHub.cs`):
   - Inherits `Hub`
   - No custom methods needed initially — server pushes to clients
   - `sealed class` with XML doc comments

3. **Create `TrafficHubNotifier`** (`HttpLens.Dashboard/Hubs/TrafficHubNotifier.cs`):
   - Subscribes to `ITrafficStore.OnRecordAdded` event (already exists on the interface)
   - Injects `IHubContext<TrafficHub>` to broadcast new records
   - Implements `IHostedService` to manage subscription lifecycle
   - On record added: `await hubContext.Clients.All.SendAsync("RecordAdded", record)`
   - `sealed class` with primary constructor

4. **Register SignalR** in `ServiceCollectionExtensions.AddHttpLens()`:
   - Call `services.AddSignalR()` (idempotent — safe if host app already added it)
   - Register `TrafficHubNotifier` as hosted service

5. **Map the hub endpoint** in `EndpointRouteBuilderExtensions.MapHttpLensDashboard()`:
   - `endpoints.MapHub<TrafficHub>($"{path}/hub")`
   - Apply the same security endpoint filter (API key, IP allowlist, enabled guard)

6. **Security**: The SignalR hub must respect:
   - Master switch (`IsEnabled`)
   - API key (via query string `?key=` on WebSocket connection — SignalR standard pattern)
   - IP allowlist
   - Authorization policy

#### Frontend

7. **Add SignalR client library**:
   - Add `@microsoft/signalr` npm dependency to `dashboard-ui/package.json`
   - Update esbuild config to bundle it

8. **Create `SignalRService`** (`services/signalr.service.ts`):
   - Builds `HubConnection` to `{basePath}/hub`
   - Includes API key as query parameter if present
   - On `"RecordAdded"` event: call `store.prependRecord(record)`
   - Auto-reconnect with exponential backoff
   - Update `store.setConnectionStatus()` on connect/disconnect/reconnect
   - Falls back to polling if SignalR connection fails

9. **Update `index.ts`**:
   - Replace `PollingService` with `SignalRService` as primary data transport
   - Keep polling as fallback when SignalR is unavailable
   - Initial data still fetched via REST API

10. **Update `ConnectionStatus` handling**:
    - Show "⚡ Live" when SignalR connected
    - Show "🔄 Polling" when falling back to polling
    - Show "⚠️ Disconnected" when both fail

#### Tests

11. Create tests for:
    - `TrafficHub` maps correctly and is accessible
    - `TrafficHubNotifier` broadcasts on `OnRecordAdded`
    - Security middleware applies to hub endpoint (API key, IP, enabled guard)
    - Fallback to polling when SignalR unavailable

---

### FEATURE 2: SQLite Persistence

Add optional SQLite storage backend so traffic records survive app restarts.

#### Backend

1. **Add SQLite packages** to `Directory.Packages.props`:
   - `Microsoft.Data.Sqlite` (appropriate version)
   - Do NOT use EF Core — use raw ADO.NET for minimal footprint

2. **Create `SqliteTrafficStore`** (`HttpLens.Core/Storage/SqliteTrafficStore.cs`):
   - Implements `ITrafficStore`
   - `sealed class` with primary constructor accepting `IOptions<HttpLensOptions>`
   - Uses `Microsoft.Data.Sqlite.SqliteConnection`
   - On first use, creates the database file and schema:
     ```sql
     CREATE TABLE IF NOT EXISTS TrafficRecords (
       Id TEXT PRIMARY KEY,
       Timestamp TEXT NOT NULL,
       Duration TEXT NOT NULL,
       HttpClientName TEXT NOT NULL,
       RequestMethod TEXT NOT NULL,
       RequestUri TEXT NOT NULL,
       RequestHeaders TEXT,          -- JSON serialized
       RequestBody TEXT,
       RequestContentType TEXT,
       RequestBodySizeBytes INTEGER,
       ResponseStatusCode INTEGER,
       ResponseHeaders TEXT,         -- JSON serialized
       ResponseBody TEXT,
       ResponseContentType TEXT,
       ResponseBodySizeBytes INTEGER,
       IsSuccess INTEGER NOT NULL,
       Exception TEXT,
       TraceId TEXT,
       ParentSpanId TEXT,
       InboundRequestPath TEXT,
       AttemptNumber INTEGER NOT NULL DEFAULT 1,
       RetryGroupId TEXT
     );
     CREATE INDEX IF NOT EXISTS IX_TrafficRecords_Timestamp ON TrafficRecords(Timestamp);
     CREATE INDEX IF NOT EXISTS IX_TrafficRecords_RetryGroupId ON TrafficRecords(RetryGroupId);
     ```
   - `Add()`: INSERT with JSON-serialized headers, fire `OnRecordAdded` event
   - `GetAll()`: SELECT ordered by Timestamp DESC, deserialize headers from JSON
   - `GetById()`: SELECT WHERE Id = @id
   - `GetByRetryGroupId()`: SELECT WHERE RetryGroupId = @groupId
   - `Clear()`: DELETE FROM TrafficRecords
   - `Count`: SELECT COUNT(*)
   - Ring-buffer behavior: after INSERT, DELETE oldest rows when count > `MaxStoredRecords`
   - All methods must be thread-safe (use connection pooling or synchronization)
   - Connection string: `Data Source={DatabasePath};Cache=Shared`

3. **Add options** to `HttpLensOptions`:
   - `bool EnableSqlitePersistence` (default `false`)
   - `string SqliteDatabasePath` (default `"httplens.db"`)

4. **Update `ServiceCollectionExtensions.AddHttpLens()`**:
   - When `EnableSqlitePersistence` is true, register `SqliteTrafficStore` instead of `InMemoryTrafficStore` as the `ITrafficStore` singleton
   - When false, use `InMemoryTrafficStore` (backward compatible)

5. **Add migration/schema versioning**:
   - Create a `schema_version` table to track schema version
   - On startup, check version and apply any pending migrations
   - v1 migration: create the `TrafficRecords` table + indexes

#### Tests

6. Create comprehensive tests for:
    - `SqliteTrafficStore` CRUD operations (Add, GetAll, GetById, GetByRetryGroupId, Clear, Count)
    - Ring-buffer eviction (add more than `MaxStoredRecords`, verify oldest removed)
    - JSON serialization/deserialization of headers
    - Thread safety (concurrent Add calls)
    - `OnRecordAdded` event fires after Add
    - Schema creation on first use
    - DI registration: `EnableSqlitePersistence = true` → `SqliteTrafficStore`
    - DI registration: `EnableSqlitePersistence = false` → `InMemoryTrafficStore`
    - SQLite database file is created at configured path

---

### FEATURE 3: Enhanced Dashboard — Real-Time Connection UI

Update the dashboard to support the new SignalR connection and display connection state prominently.

#### Frontend

1. **Update `types/traffic.ts`**:
   - Add `'live'` to `ConnectionStatus` type: `'connected' | 'live' | 'reconnecting' | 'disconnected'`

2. **Create connection status indicator component** (`components/connection-indicator.ts`):
   - Shows connection mode icon + text in the header
   - `⚡ Live` (green) — SignalR connected
   - `🔄 Polling` (yellow) — Polling fallback active
   - `⚠️ Disconnected` (red) — No connection
   - `🔁 Reconnecting...` (orange) — SignalR reconnecting

3. **Update `store.ts`**:
   - Add `connectionMode: 'signalr' | 'polling' | 'none'` to state
   - New method: `setConnectionMode(mode)` for components to read

4. **Update dashboard HTML**:
   - Add connection indicator element in the header bar
   - Add SignalR status details tooltip on hover

#### Tests

5. Frontend-level tests:
   - Connection indicator renders correct state for each mode
   - Store correctly tracks connection mode transitions

---

## v1.5 Features — "Advanced Protocol & Visualization" (6–8 weeks)

> Theme: gRPC support, deep visualization, analytics, binary body support

### FEATURE 4: gRPC Call Support (New HttpLens.Grpc Package)

#### Deep-Dive Analysis

gRPC calls in .NET go through `Grpc.Net.Client`, which internally uses `HttpClient`. This means HttpLens already captures gRPC traffic as raw HTTP/2 POST requests — but the data is opaque: method shows `POST`, URI is `/PackageName.ServiceName/MethodName`, body is binary protobuf, and status is always 200 (gRPC errors are in trailers, not HTTP status).

The proper approach is a `Grpc.Net.Client.Interceptor` — the official gRPC extensibility point. This gives typed request/response messages (before protobuf serialization), gRPC status codes, trailers, and deadline metadata.

**Deduplication** is critical: since gRPC uses `HttpClient` underneath, the same request would be captured by both `HttpLensDelegatingHandler` (as raw HTTP/2) and `GrpcLensInterceptor` (as typed gRPC). The interceptor must set a header flag that `HttpLensDelegatingHandler` checks to skip double-capture (same pattern as `DiagnosticInterceptor`).

**HTTP/2 frame capture** is NOT feasible at the application layer — .NET's `HttpClient` abstracts away HTTP/2 framing. The gRPC interceptor captures at the gRPC semantics level, which is far more useful.

**Business Value**: No existing .NET library provides a unified HTTP + gRPC traffic dashboard. This is a competitive moat.

#### Backend

1. **Add new properties to `HttpTrafficRecord`** (`Models/HttpTrafficRecord.cs`):
   - `string? GrpcServiceName` — the gRPC service name (e.g., `"greeter.Greeter"`)
   - `string? GrpcMethodName` — the gRPC method name (e.g., `"SayHello"`)
   - `int? GrpcStatusCode` — the `Grpc.Core.StatusCode` enum cast to int (0=OK, 1=Cancelled, etc.)
   - `string? GrpcStatusDetail` — the status detail/message string
   - `bool IsGrpc` — computed property: `=> GrpcServiceName is not null`

2. **Create new project**: `src/HttpLens.Grpc/`
   - `HttpLens.Grpc.csproj` referencing `HttpLens.Core`, `Grpc.Net.Client` (≥ 2.62.0), `Google.Protobuf` (≥ 3.26.0)
   - Add package versions to `Directory.Packages.props`

3. **Create `GrpcLensInterceptor`** (`: Grpc.Core.Interceptors.Interceptor`):
   - Override `AsyncUnaryCall<TRequest, TResponse>`: wrap the call, capture before/after
   - Override `AsyncServerStreamingCall<TRequest, TResponse>`: capture initial request and first response or completion
   - Override `AsyncClientStreamingCall<TRequest, TResponse>`: capture completion
   - Override `AsyncDuplexStreamingCall<TRequest, TResponse>`: capture completion
   - For each call type:
     a. Create `HttpTrafficRecord` with:
        - `Timestamp = DateTimeOffset.UtcNow`
        - `RequestMethod = "gRPC"` (or `"POST"` for realism)
        - `RequestUri = $"grpc://{host}/{serviceName}/{methodName}"` (use `method.FullName`)
        - `GrpcServiceName = method.ServiceName`, `GrpcMethodName = method.Name`
        - `HttpClientName = "(grpc)"`
        - `RequestBody = serialize request to JSON` using `Google.Protobuf.JsonFormatter.Default` if message implements `IMessage`; otherwise `ToString()`
        - `RequestContentType = "application/grpc+json"` (for display)
        - `TraceId`, `ParentSpanId` from `Activity.Current`
     b. Start `Stopwatch` before invoking base call
     c. On successful response: serialize response to JSON, set `GrpcStatusCode = 0`, `ResponseStatusCode = 200`, `IsSuccess = true`
     d. On `RpcException`: set `GrpcStatusCode = (int)ex.StatusCode`, `GrpcStatusDetail = ex.Status.Detail`, `IsSuccess = false`, map gRPC status to approximate HTTP code (NotFound→404, PermissionDenied→403, etc.)
     e. Store via `ITrafficStore.Add(record)`
   - **Deduplication**: Set header `"x-httplens-grpc-captured"` on `CallOptions.Headers` so `HttpLensDelegatingHandler` can skip the underlying HTTP/2 POST
   - Update `HttpLensDelegatingHandler.SendAsync()` to check for this header and skip if present
   - Apply `SensitiveHeaderMasker` to request/response metadata before storing

4. **Create extension methods** (`HttpLens.Grpc/Extensions/GrpcExtensions.cs`):
   - `public static IServiceCollection AddGrpcInterception(this IServiceCollection services)` — Register `GrpcLensInterceptor` as transient, configure `GrpcClientFactoryOptions` to add interceptor
   - Enable fluent: `services.AddHttpLens().AddGrpcInterception()`

5. **Update `TrafficFilterCriteria`** to support protocol filtering:
   - Add `string? Protocol` field (values: `"http"`, `"grpc"`, or null for all)
   - Update `TrafficFilter.Apply()` to filter by `IsGrpc` when `Protocol` specified
   - Update `GET /api/traffic` to accept `?protocol=grpc` query parameter

#### Frontend

6. **Update `types/traffic.ts`**:
   - Add: `grpcServiceName`, `grpcMethodName`, `grpcStatusCode`, `grpcStatusDetail`, `isGrpc` (camelCase)

7. **Update `traffic-table.ts` `renderRow()`**:
   - When `record.isGrpc === true`:
     a. Render purple `"gRPC"` badge (CSS class `"method-grpc"`) instead of HTTP method badge
     b. Show `GrpcServiceName/GrpcMethodName` in URL column
     c. Map gRPC status codes to `StatusClass`: 0=success, 1-4=client-error, 5-16=server-error

8. **Update `detail-panel.ts`**:
   - Request tab: for gRPC records, show service name, method name, JSON-serialized protobuf body
   - Response tab: show gRPC status code + detail alongside JSON response body
   - Correlation tab: show gRPC-specific metadata

9. **Update `formatters.ts`**:
   - Add `getGrpcStatusClass(code: number | null): StatusClass`
   - Add `formatGrpcStatus(code: number | null): string` (maps code to name: "OK", "NOT_FOUND", etc.)

10. **Add protocol filter to `filter-bar.ts`**:
    - "Protocol" dropdown: All, HTTP, gRPC
    - Wire to store's filter state

#### Tests

11. Create `tests/HttpLens.Grpc.Tests/` project:
    - GrpcLensInterceptor captures unary calls with correct service/method names
    - Request/response protobuf JSON serialization
    - gRPC error status codes captured correctly
    - Deduplication (underlying HTTP not double-captured)
    - Sensitive header masking on gRPC metadata
    - DI registration with `AddGrpcInterception()`
    - Protocol filtering in TrafficFilter

---

### FEATURE 5: Performance Flame Chart

#### Deep-Dive Analysis

The data needed is already captured: every `HttpTrafficRecord` has `Timestamp` (start time) and `Duration` (width). The flame chart is a pure frontend visualization — no backend changes needed.

The lane assignment algorithm packs concurrent requests into swim lanes: sort by start time, for each record find the lowest lane where it doesn't overlap the last record in that lane (greedy left-to-right packing).

Canvas rendering is preferred over SVG for performance at 100+ records. Color by status class using existing `getStatusClass()` mapping.

**Business Value**: Instantly reveals sequential vs. parallel request patterns. A developer can see "these 5 API calls happen one after another, taking 2s total — they could be parallelized to 400ms."

#### Frontend (No backend changes needed)

1. **Create `components/flame-chart.ts`**:
   - Constructor accepts container element
   - `init()`: create `<canvas>`, attach `ResizeObserver`, subscribe to store changes
   - `render()`: redraws on every store change
   - Data preparation:
     a. Get records from `store.getFilteredRecords()`
     b. Parse each record's timestamp to epoch ms, parse duration via `parseDurationMs()`
     c. Compute `globalStartMs = min(all starts)`, `globalEndMs = max(all start + duration)`
   - **Lane assignment**:
     a. Sort records by start time
     b. For each record, find lowest lane where record doesn't overlap last record in that lane
     c. Store lane index per record
   - **Canvas drawing**:
     a. `xScale = canvasWidth / totalRangeMs`
     b. `laneHeight = 24px`
     c. For each record: `x = (startMs - globalStartMs) * xScale`, `y = lane * laneHeight`, `width = max(durationMs * xScale, 2)`, fill color by status class
     d. For gRPC records (`isGrpc`): use distinct purple (#a855f7)
     e. Draw time axis at top with tick marks
   - **Interaction**:
     a. Canvas `click`: hit-test bars, call `store.selectRecord(id)`
     b. Canvas `mousemove`: show tooltip with method, URL, duration
     c. Highlight selected record's bar
   - **Zoom/pan** (optional): mouse wheel zooms, click+drag pans, double-click resets

2. **Update `index.ts`**:
   - Import `FlameChart`, add view toggle: "Table" vs "Flame" (default: Table)
   - When "Flame" active, hide traffic table, show flame chart container

3. **Update dashboard HTML**:
   - View toggle button group: `[📋 Table] [🔥 Flame]`
   - `<div id="flame-chart-container" class="hidden"></div>`

4. **Add CSS** for flame chart: canvas sizing, tooltip styling, theme support

#### Tests

5. Test lane assignment algorithm in isolation:
   - Non-overlapping records → same lane
   - Overlapping records → different lanes
   - Complex scenario with 10 records → no visual overlaps
6. Test view toggle state management
7. Test click-to-select integration with store

---

### FEATURE 6: Request Flow Visualization

#### Deep-Dive Analysis

The existing `HttpTrafficRecord` already captures `TraceId`, `ParentSpanId`, and `InboundRequestPath`. These three fields are sufficient to build a call tree:

- Group records by `TraceId` — each trace ID = one end-to-end request flow
- Root node = record with earliest Timestamp in the trace group
- Build parent→child using `ParentSpanId` matching
- Fallback heuristic: group by `InboundRequestPath` + order by timestamp

**Business Value**: "No competitor offers this for in-process HttpClient debugging." New team members can see "when I hit POST /api/checkout, the app calls inventory-service, payment-service, and notification-service in sequence" — without reading any code.

#### Backend

1. **Add new API endpoint** in `TrafficApiEndpoints.cs`:
   - `GET /api/traffic/flows` → records grouped by TraceId:
     ```json
     { "flows": [{ "traceId": "...", "rootPath": "POST /api/checkout", "recordCount": 4, "startTime": "...", "totalDurationMs": 1234, "records": [...] }] }
     ```
   - Skip records where TraceId is null
   - Order flows by most recent first
   - Apply same security endpoint filter

2. **Add helper** for building flow trees:
   - `FlowNode`: `{ record, children[], depth }`
   - Build tree using ParentSpanId relationships or timestamp heuristic fallback

#### Frontend

3. **Create types** in `types/traffic.ts`:
   - `FlowNode: { record, children, depth }`
   - `FlowGroup: { traceId, rootPath, recordCount, startTime, totalDurationMs }`
   - Add `'flows'` to `DetailTab` type

4. **Create `components/flow-panel.ts`**:
   - Fetches flow groups from `/api/traffic/flows`
   - Left sidebar: list of flow groups (rootPath, recordCount, timestamp)
   - Right content: selected flow as SVG tree diagram
   - **Tree rendering (SVG)**:
     a. Each node = rounded rectangle: METHOD badge + URL + status badge + duration
     b. Horizontal layout: root left, children branching right
     c. `X = depth * nodeWidth`, `Y = cumulative height of siblings`
     d. SVG `<path>` with bezier curves connecting parent→child
     e. Node colors match status classes; gRPC nodes use purple badge
   - Click node → `store.selectRecord(record.id)` to open detail panel

5. **Update dashboard**: Add `[🔀 Flows]` button to view toggle, `<div id="flow-panel-container">`

6. **Add CSS**: SVG node styling, edge/line styling, sidebar, responsive horizontal scroll

#### Tests (backend)

7. Test `GET /api/traffic/flows`:
   - Groups by TraceId correctly
   - Excludes null TraceId records
   - Returns empty when no flows
   - Security middleware applies
8. Test flow tree building:
   - Single-record flow → root, no children
   - Multi-record same TraceId → correct tree
   - Different TraceIds → separate groups

---

### FEATURE 7: Analytics / Statistics Dashboard Tab

#### Deep-Dive Analysis

Analytics are computed from the existing in-memory record array. For v1, client-side computation is sufficient. Backend provides a pre-aggregated endpoint for efficiency with SQLite persistence.

**Business Value**: Raw traffic data is overwhelming. P95 latency by host immediately tells you "payment-service is your bottleneck at 800ms P95." Error rate by endpoint pinpoints unreliable API calls.

#### Backend

1. **Add `GET /api/traffic/stats` endpoint** to `TrafficApiEndpoints.cs`:
   - Computes from `ITrafficStore.GetAll()`:
     ```json
     {
       "totalRecords": 500,
       "byHost": [{ "host": "api.github.com", "count": 42, "avgMs": 120, "p50Ms": 100, "p95Ms": 340, "p99Ms": 780, "errorRate": 0.05, "errorCount": 2 }],
       "byStatusCode": { "2xx": 180, "3xx": 5, "4xx": 12, "5xx": 3, "error": 1 },
       "byMethod": { "GET": 120, "POST": 80 },
       "timeline": [{ "bucket": "2024-01-15T10:00:00", "count": 15, "avgMs": 200, "errorCount": 1 }],
       "slowestEndpoints": [{ "method": "POST", "host": "payment.api", "path": "/charge", "p95Ms": 800, "count": 25 }],
       "protocolBreakdown": { "http": 450, "grpc": 50 }
     }
     ```
   - Timeline: ~50 equal buckets across time range
   - Percentile: sort durations, pick `Math.ceil(percentile/100 * count) - 1`
   - Apply security pipeline, `.ExcludeFromDescription()`

#### Frontend

2. **Create types** in `types/traffic.ts`: `AnalyticsResponse`, `HostStats`, `TimelineBucket`, `EndpointStats`

3. **Add `fetchStats()`** to `traffic-api.service.ts`

4. **Create `components/analytics-panel.ts`**:
   - Fetches stats on init + 10-second refresh
   - **Summary cards** (top row): Total Requests, Overall Error Rate (color-coded), Average Response Time, Protocol breakdown
   - **Response Time by Host** (sortable table): Host, Count, Avg, P50, P95, P99, Error Rate — color-coded P95 values
   - **Status Code Distribution** (SVG donut chart): 2xx/3xx/4xx/5xx/Error segments with percentages
   - **Request Volume Timeline** (SVG bar chart): time buckets, stacked success/error bars, hover tooltips
   - **Slowest Endpoints** (table): Method, Host, Path, P95, Count — top 10
   - **Method Distribution** (horizontal bar chart): one bar per method, width proportional to count

5. **Update dashboard**: Add `[📊 Analytics]` button to view toggle, `<div id="analytics-container">`

6. **Add CSS**: Card layout (flexbox), chart styling for dark/light themes, responsive stacking

#### Tests (backend)

7. Test `GET /api/traffic/stats`:
   - Correct total count
   - Groups by host correctly
   - Percentile computation with known durations
   - Error rate computation
   - Timeline bucketing
   - Empty stats when store empty
   - Security middleware applies
   - Protocol breakdown counts HTTP/gRPC separately

---

### FEATURE 8: Binary Body Support

#### Deep-Dive Analysis

Currently, `BodyCapture.CaptureAsync()` calls `content.ReadAsStringAsync()` which fails silently for binary content (garbled text or throws). Binary APIs (images, PDFs, protobuf) appear as "No response body" — misleading.

Detection strategy: check `content.Headers.ContentType.MediaType` before reading. Text types (`text/*`, `application/json`, `application/xml`, `*+json`, `*+xml`) use string capture. Everything else uses byte[] → Base64 capture.

**Business Value**: Complete coverage of all API response types. Image generation API responses visible inline. No data loss for binary bodies.

#### Backend

1. **Add new properties to `HttpTrafficRecord`**:
   - `bool IsRequestBodyBinary { get; set; }`
   - `bool IsResponseBodyBinary { get; set; }`
   - `string? RequestBodyBase64 { get; set; }` — base64-encoded binary body
   - `string? ResponseBodyBase64 { get; set; }` — base64-encoded binary body

2. **Create `BodyCapture.IsBinaryContentType(string? contentType) : bool`**:
   - Returns `false` for: `text/*`, `application/json`, `application/xml`, `application/javascript`, `application/x-www-form-urlencoded`, any type ending in `+json` or `+xml`
   - Returns `true` for everything else: `image/*`, `application/octet-stream`, `application/pdf`, `application/protobuf`, `audio/*`, `video/*`, etc.
   - `null`/empty → `false` (assume text)

3. **Update `BodyCapture.CaptureAsync()`** to handle binary:
   - Check content type via `IsBinaryContentType()`
   - If text: existing behavior (ReadAsStringAsync, truncate, store in `RequestBody`/`ResponseBody`)
   - If binary:
     a. Read as `byte[]` via `content.ReadAsByteArrayAsync()`
     b. Respect `MaxBodyCaptureSize` (truncate bytes)
     c. Convert to base64, store in `RequestBodyBase64`/`ResponseBodyBase64`
     d. Set `IsRequestBodyBinary`/`IsResponseBodyBinary = true`
     e. Set `RequestBody`/`ResponseBody = null` (don't store garbled text)
     f. Set size bytes = byte array length

4. **Update `HttpLensDelegatingHandler.SendAsync()`** to use updated BodyCapture and set binary fields

#### Frontend

5. **Update `types/traffic.ts`**: Add `isRequestBodyBinary`, `isResponseBodyBinary`, `requestBodyBase64`, `responseBodyBase64`

6. **Create `utils/binary.ts`**:
   - `base64ToHexDump(base64, bytesPerLine = 16): string` — formatted hex dump: `"00000000  48 65 6c 6c...  |Hello...|"`
   - `base64ToDataUrl(base64, mimeType): string` — `"data:{mimeType};base64,{base64}"`
   - `isImageContentType(contentType): boolean` — true for `image/png`, `image/jpeg`, `image/gif`, `image/webp`, `image/svg+xml`

7. **Update `detail-panel.ts`** `renderRequestTab()` and `renderResponseTab()`:
   - If binary AND image content type: render `<img>` with `src=base64ToDataUrl(...)` + "Download" button
   - If binary AND NOT image: render hex dump + "Download" button + toggle "Hex" | "Base64"
   - If NOT binary: existing behavior (text/JSON pretty-print)

8. **Add CSS**: Hex dump monospace styling, image preview sizing, download button

#### Tests (backend)

9. Test `IsBinaryContentType()`:
   - `text/plain` → false, `application/json` → false, `application/vnd.api+json` → false
   - `image/png` → true, `application/octet-stream` → true, `application/pdf` → true
   - `null` → false, empty → false

10. Test `BodyCapture` with binary content:
    - Binary captured as base64, `MaxBodyCaptureSize` respected
    - Text still captured as string (backward compatible)
    - `IsRequestBodyBinary`/`IsResponseBodyBinary` flags correct

11. Test `HttpLensDelegatingHandler` with binary responses:
    - Image response → base64, text response → string (regression)

---

## General Guidelines (Apply to ALL Features)

### Code Style
- Follow existing conventions exactly: file-scoped namespaces, primary constructors, sealed classes, XML doc comments on all public APIs
- All new NuGet package references → `Directory.Packages.props`
- Run existing tests after each feature to ensure no regressions
- Build TypeScript with existing esbuild setup

### Documentation
- Update `CHANGELOG.md` with `[1.3.0]` and `[1.5.0]` sections
- Update `README.md` with new configuration options, API endpoints, feature descriptions

### Security
- All new API endpoints go through existing security endpoint filter in `MapHttpLensApi`
- New dependencies: check for known security advisories before adding
- SignalR hub must respect all security layers

### Dashboard Views
- Views (Table, Flame, Flows, Analytics) are mutually exclusive — only one active at a time
- All canvas/SVG rendering must support both dark and light themes
- Flame chart and flow visualization must be performant with 500+ records

### Backward Compatibility
- Binary body capture must not break existing text body behavior
- SQLite persistence is opt-in (default off)
- SignalR connection falls back to polling
- All existing tests must continue to pass

### Recommended Implementation Order
1. **Binary Body Support** (Feature 8) — simplest, no new packages, foundational for gRPC
2. **SQLite Persistence** (Feature 2) — independent, backend-only
3. **SignalR Real-Time Push** (Feature 1) — depends on store interface
4. **Enhanced Dashboard Connection UI** (Feature 3) — depends on SignalR
5. **gRPC Call Support** (Feature 4) — new package, depends on binary support
6. **Analytics Dashboard** (Feature 7) — backend + frontend, independent of visualization
7. **Performance Flame Chart** (Feature 5) — pure frontend
8. **Request Flow Visualization** (Feature 6) — most complex, depends on working TraceId correlation
