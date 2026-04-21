# Prompt for AI Coding Agent: Build HttpLens v1.5 Features

> **Standalone v1.5 milestone prompt only — "Advanced Protocol & Visualization".**
> Assumes v1.3 features are already implemented.

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
- `Models/HttpTrafficRecord.cs` — central traffic model (+ HAR models, retry fields)
- `Configuration/HttpLensOptions.cs` — options (`MaxStoredRecords`, body capture, security, URL patterns)
- `Storage/ITrafficStore.cs` / `Storage/InMemoryTrafficStore.cs` — store contract + in-memory ring buffer
- `Interceptors/HttpLensDelegatingHandler.cs` — primary HTTP capture for `IHttpClientFactory`
- `Interceptors/DiagnosticInterceptor.cs` / `DiagnosticInterceptorHostedService.cs` — manual `HttpClient` capture
- `Interceptors/RetryDetectionHandler.cs` — retry grouping metadata on requests
- `Interceptors/BodyCapture.cs` / `HeaderSnapshot.cs` / `SensitiveHeaderMasker.cs` / `UrlPatternMatcher.cs` — body/header/masking/filter helpers
- `Filtering/TrafficFilterCriteria.cs` / `Filtering/TrafficFilter.cs` — server-side filtering
- `Export/CurlExporter.cs` / `CSharpExporter.cs` / `HarExporter.cs` — export generators
- `Extensions/ServiceCollectionExtensions.cs` / `HttpClientBuilderExtensions.cs` — DI + handler registration

### HttpLens.Dashboard
- `Api/TrafficApiEndpoints.cs` — dashboard API endpoints (`/api/traffic`, by id, clear, retry group, export)
- `Extensions/EndpointRouteBuilderExtensions.cs` — mounts SPA + API and applies security filters/pipeline
- Middleware: `EnabledGuardMiddleware`, `IpAllowlistMiddleware`, `ApiKeyAuthMiddleware`, `DashboardMiddleware`

### TypeScript SPA (`dashboard-ui/src/`)
- `types/traffic.ts` — shared client contracts (records, filters, tabs, connection)
- `state/store.ts` — app state + subscriptions + mutations
- Services: `services/traffic-api.service.ts`, `services/polling.service.ts`
- Components: `components/traffic-table.ts`, `components/detail-panel.ts`, `components/filter-bar.ts`, `components/exporters.ts`
- Utilities: `utils/formatters.ts`, `utils/html.ts`, `utils/dom.ts`
- Entry: `index.ts` bootstraps UI, events, fetch/polling, theme, exports

### Build & Package Management
- `Directory.Build.props` — `LangVersion=latest`, nullable enabled, TFMs net8/net9/net10
- `Directory.Packages.props` — centralized NuGet versions

### Coding Conventions (MUST follow)
- File-scoped namespaces, primary constructors, sealed classes, XML docs on public APIs
- Use `IOptionsMonitor<HttpLensOptions>` for runtime options
- Keep API endpoints `.ExcludeFromDescription()` and preserve dashboard security pipeline
- Add new package versions in `Directory.Packages.props`

## Engineering Standards & Principles

### Testing Strategy

#### Unit Tests
Every new class and non-trivial method must have isolated, deterministic tests.

**C# Unit Test Rules (xUnit + Moq)**
- Place tests in matching `tests/*` project and use `{ClassName}Tests`
- Use Arrange/Act/Assert, `[Fact]`/`[Theory]`, and mock interfaces only
- Avoid sleeps/flaky timing; use TestServer for API integration-style coverage
- Target high coverage on new code

**TypeScript Unit Test Rules (Vitest or Jest)**
- Add tests per source file; test public behavior and rendering output contracts
- Utility modules require complete branch coverage

**What must be unit-tested (minimum bar per feature):**

| Feature | C# must-test | TypeScript must-test |
|---------|-------------|---------------------|
| gRPC (F4) | `GrpcLensInterceptor` for all 4 call types; dedup header; status mapping | `getGrpcStatusClass` all 17 status codes; `formatGrpcStatus` |
| Flame Chart (F5) | — | Lane-assignment algorithm; viewport scaling; click hit-test |
| Flow Viz (F6) | Flow tree builder; `GET /api/traffic/flows` grouping | `FlowPanel` renders sidebar items; tree node layout |
| Analytics (F7) | Percentile computation; timeline bucketing; error rate; `GET /api/traffic/stats` | Summary card values; chart data mapping |
| Binary Body (F8) | `IsBinaryContentType` all branches; `BodyCapture` text vs binary path | `base64ToHexDump` format; `isImageContentType`; render branch selection |

### Automated Tests & CI
- Run `dotnet test`; add/update CI for any new test projects
- Run frontend tests/build/type-check (`npm test`, `npm run build`, `tsc --noEmit`) where applicable
- Publish coverage artifacts in CI

### C# / .NET Best Practices
- Use modern C# (`LangVersion=latest`), nullable annotations, explicit access modifiers
- Prefer async flows with `CancellationToken`; avoid `.Result`/`.Wait()`
- Use DI with abstractions and correct lifetimes; avoid service locator
- Validate inputs and avoid swallowed exceptions
- Parameterize SQL and avoid unnecessary allocations in hot paths
- Keep XML documentation on public/internal APIs

### TypeScript Best Practices
- Use strict typing (`"strict": true`), avoid `any`, explicit signatures
- Use modern syntax (`const`, destructuring, template literals, optional chaining)
- Keep modules/components single-responsibility and testable
- Handle fetch failures and sanitize displayed errors
- Optimize rendering paths (debounce/filter and redraw discipline)

### SOLID Principles
- Apply SRP/OCP/LSP/ISP/DIP in all new design decisions
- Keep storage abstractions interchangeable through `ITrafficStore`
- Add behavior via extensions/abstractions rather than tightly-coupled rewrites

### Object-Oriented Principles
- Enforce encapsulation and immutability for model-like data
- Prefer composition over inheritance (except framework-required base types)
- Keep domain logic separate from presentation logic

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

## General Guidelines

### Code Style
- Follow existing conventions exactly: file-scoped namespaces, primary constructors, sealed classes, XML doc comments on all public APIs
- All new NuGet package references → `Directory.Packages.props`
- Run existing tests after each feature to ensure no regressions
- Build TypeScript with existing esbuild setup

### Engineering Principles Checklist

Before submitting any feature for review, verify all of the following. These are non-negotiable acceptance criteria derived from the [Engineering Standards & Principles](#engineering-standards--principles) section above.

#### Testing
- [ ] Every new C# class has a `{ClassName}Tests` file in the corresponding `*.Tests` project
- [ ] Every test method follows `{Method}_{Condition}_{ExpectedOutcome}` naming
- [ ] All tests are fully isolated — no real file I/O, no real HTTP calls, no `Thread.Sleep`
- [ ] `Moq` is used to mock all interface dependencies; concrete classes are never mocked
- [ ] Integration tests use `TestServer` / `WebApplicationFactory<T>` for Minimal API endpoints
- [ ] TypeScript tests added for all new utility functions and components
- [ ] `coverlet` coverage for new C# code is ≥ 90% line coverage
- [ ] `dotnet test` passes with zero failures before opening a PR
- [ ] `npm test` (TypeScript) passes with zero failures before opening a PR

#### C# Best Practices
- [ ] No `async void` methods (except event handlers — and even those must be wrapped with error handling)
- [ ] `CancellationToken` threaded through every async call chain
- [ ] `ConfigureAwait(false)` on all `await` calls inside library projects
- [ ] No `.Result` or `.Wait()` — all async code is `await`-ed
- [ ] No service-locator calls (`IServiceProvider.GetService<T>()`) inside business logic
- [ ] All nullable reference types annotated with `?`; no `#nullable disable` pragmas
- [ ] `ArgumentNullException.ThrowIfNull` used for public API parameter validation
- [ ] SQL queries use parameterised commands — no string interpolation in SQL
- [ ] Compiled regex patterns use `[GeneratedRegex]` or `static readonly` fields

#### TypeScript Best Practices
- [ ] `"strict": true` in `tsconfig.json` — zero `any` types, no implicit nulls
- [ ] No `var`; `const` for everything that is not reassigned
- [ ] All exported function signatures have explicit parameter and return type annotations
- [ ] `readonly` arrays used for data flowing into render functions
- [ ] Debounce applied to text filter inputs
- [ ] `assertNever()` guard in exhaustive `switch` statements over union types

#### SOLID
- [ ] Each new class has exactly one reason to change (SRP)
- [ ] New behaviour is added by implementing an interface or extending a record, not by modifying existing classes (OCP)
- [ ] No interface has methods that some implementors don't need (ISP)
- [ ] All constructor parameters are typed as interfaces or abstractions, not concrete classes (DIP)

#### OOP
- [ ] No mutable public fields — all state changes go through methods
- [ ] Domain concepts expressed as named types, not primitives or magic strings
- [ ] Composition used instead of inheritance wherever the framework allows
- [ ] New model types use `init`-only setters or positional records for immutability
- [ ] Rendering/presentation logic is separated from domain/computation logic

### Documentation
- Update `CHANGELOG.md` with `[1.5.0]` section
- Update `README.md` with new configuration options, API endpoints, feature descriptions

### Security
- All new API endpoints go through existing security endpoint filter in `MapHttpLensApi`
- New dependencies: check for known security advisories before adding
- SignalR hub must respect all security layers

### Dashboard Views
- Views for this milestone are focused on: Flame, Flows, and Analytics
- Views are mutually exclusive — only one active at a time
- Canvas/SVG rendering must support both dark and light themes

### Backward Compatibility
- Binary body capture must not break existing text body behavior
- All existing tests must continue to pass

### Recommended Implementation Order
1. **Binary Body Support** (Feature 8)
2. **gRPC Call Support** (Feature 4)
3. **Analytics Dashboard** (Feature 7)
4. **Performance Flame Chart** (Feature 5)
5. **Request Flow Visualization** (Feature 6)
