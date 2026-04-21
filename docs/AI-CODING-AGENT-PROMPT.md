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

## Engineering Standards & Principles

> These standards apply to **every line of code** written for v1.3 and v1.5. They are not aspirational — they are mandatory acceptance criteria. Code that violates them should not be merged.

---

### Testing Strategy

#### Unit Tests

Every new class and every non-trivial method must have a corresponding unit test. Unit tests are fast (< 1 ms), fully isolated (no I/O, no real HTTP calls), and test one behaviour at a time.

**C# Unit Test Rules (xUnit + Moq)**

- Place tests in the matching `*.Tests` project under `tests/`:
  - `HttpLens.Core.Tests` — all classes in `HttpLens.Core`
  - `HttpLens.Dashboard.Tests` — all classes in `HttpLens.Dashboard`
  - `HttpLens.Grpc.Tests` — all classes in `HttpLens.Grpc` (new project)
- One test class per production class, named `{ClassName}Tests`
- One test method per scenario, named `{MethodUnderTest}_{Condition}_{ExpectedOutcome}` (e.g., `Add_WhenAtCapacity_EvictsOldestRecord`)
- Use `[Fact]` for single-case tests and `[Theory]` + `[InlineData]` / `[MemberData]` for parameterised cases
- Use `Moq` to mock all dependencies declared through interfaces — **never** mock concrete classes
- Arrange / Act / Assert structure in every test body; separate each phase with a blank line and an `// Arrange`, `// Act`, `// Assert` comment
- Assert one logical outcome per test; use `Assert.Multiple` (xUnit) only when multiple assertions describe the same single behaviour
- Never use `Thread.Sleep` in tests — use `Task.CompletedTask`, `CancellationToken.None`, or fake clocks
- Use `Microsoft.AspNetCore.TestHost.TestServer` for integration-style tests against Minimal API endpoints
- Aim for ≥ 90% line coverage on new code; use `coverlet.collector` (already in `Directory.Packages.props`) to verify

**TypeScript Unit Test Rules (Vitest or Jest)**

- Add a test runner (`vitest` recommended for esbuild projects) to `dashboard-ui/package.json`
- One test file per source file: `{name}.test.ts` co-located under `src/` or in a parallel `__tests__/` directory
- Describe blocks mirror the class/module name; `it` / `test` blocks name the scenario
- Use `vi.fn()` / `jest.fn()` to mock external dependencies (API service, store)
- Do not test private implementation details — test only the public contract (exported functions and class public methods)
- Pure utility functions (`formatters.ts`, `html.ts`, `binary.ts`) must have 100% branch coverage
- Component rendering tests use `jsdom` and assert on generated HTML strings, not on internal state

**What must be unit-tested (minimum bar per feature):**

| Feature | C# must-test | TypeScript must-test |
|---------|-------------|---------------------|
| SignalR (F1) | `TrafficHubNotifier` broadcasts on event; hub security filter returns 401/403/404 | `SignalRService` connection lifecycle; fallback triggers polling |
| SQLite (F2) | All `ITrafficStore` operations; ring-buffer eviction; thread-safety; schema creation | n/a |
| Connection UI (F3) | — | `ConnectionIndicator` renders correct icon per state; store `setConnectionMode` |
| gRPC (F4) | `GrpcLensInterceptor` for all 4 call types; dedup header; status mapping | `getGrpcStatusClass` all 17 status codes; `formatGrpcStatus` |
| Flame Chart (F5) | — | Lane-assignment algorithm; viewport scaling; click hit-test |
| Flow Viz (F6) | Flow tree builder; `GET /api/traffic/flows` grouping | `FlowPanel` renders sidebar items; tree node layout |
| Analytics (F7) | Percentile computation; timeline bucketing; error rate; `GET /api/traffic/stats` | Summary card values; chart data mapping |
| Binary Body (F8) | `IsBinaryContentType` all branches; `BodyCapture` text vs binary path | `base64ToHexDump` format; `isImageContentType`; render branch selection |

---

#### Automated Tests & CI

**Integration Tests**

- Use `Microsoft.AspNetCore.TestHost` to start a real in-memory ASP.NET Core host for each dashboard endpoint test
- Test the full request pipeline including security middleware, not just the handler logic
- Each `TrafficApiEndpoints` endpoint must have integration tests covering: happy path, not found, security rejection (missing API key → 401, blocked IP → 403, disabled → 404)
- SQLite tests must use an in-memory SQLite database (`Data Source=:memory:`) or a `Path.GetTempFileName()` path that is deleted in `IDisposable.Dispose()`
- SignalR hub tests use `WebApplicationFactory<T>` with a WebSocket test client

**End-to-End Tests**

- Add scenarios to `HttpLens.EndToEnd.Tests` for every new feature that crosses the Core→Dashboard boundary
- Each E2E test bootstraps a `WebApplication`, makes real HTTP calls via `HttpClientFactory`, and asserts on `/api/traffic` JSON responses
- E2E tests must clean up all state in `IAsyncLifetime.DisposeAsync()`

**GitHub Actions / CI**

- All tests (`dotnet test`) and TypeScript tests (`npm test`) must pass in CI before any PR can merge
- New workflow steps must be added to the existing GitHub Actions YAML for any new test projects
- `coverlet.collector` coverage reports must be published as workflow artifacts
- TypeScript build (`npm run build`) and type-check (`tsc --noEmit`) must also run in CI with zero errors

**Test Data Builders**

- Create a `TrafficRecordBuilder` helper (test project only) using the Builder pattern to construct `HttpTrafficRecord` instances with fluent overrides:
  ```csharp
  var record = new TrafficRecordBuilder()
      .WithMethod("POST")
      .WithUri("https://api.example.com/orders")
      .WithStatusCode(201)
      .WithDuration(TimeSpan.FromMilliseconds(45))
      .Build();
  ```
- This eliminates duplicated `new HttpTrafficRecord { ... }` blocks across test files

---

### C# / .NET Best Practices

Apply these to every new and modified C# file.

#### Language & Style

- **Use the latest C# features** enabled by `LangVersion=latest` in `Directory.Build.props`:
  - Primary constructors for classes that only pass parameters to fields (already used in the codebase — continue this pattern)
  - Collection expressions (`[.. items]`) instead of `new List<T>(items)` or `.ToList()`
  - `is null` / `is not null` instead of `== null` / `!= null`
  - Pattern matching in `switch` expressions instead of long `if-else` chains
  - `nameof()` everywhere a symbol name is needed as a string
- **Nullable reference types** are enabled project-wide — annotate every nullable reference with `?`; eliminate all `#nullable disable` pragmas
- **`readonly`** fields: every field that is set only in the constructor must be `readonly`
- **`record`** types: use `record` or `readonly record struct` for immutable value-like types (e.g., `TrafficFilterCriteria` already uses `record`)
- **Explicit access modifiers**: always write `private`, `internal`, `public` — never rely on defaults

#### Async / Concurrency

- All I/O-bound operations must be `async Task` / `async Task<T>` — never `.Result` or `.Wait()`
- Pass `CancellationToken` through the full call chain; never pass `CancellationToken.None` in production code
- Prefer `ConfigureAwait(false)` in library code (HttpLens.Core, HttpLens.Dashboard) because library code should not capture the ASP.NET Core `SynchronizationContext`
- Protect shared mutable state with `SemaphoreSlim(1,1)` (async-friendly) or `ConcurrentQueue<T>` / `ConcurrentDictionary<T,V>` — never `lock` around `await`

#### Dependency Injection

- Register services with the **narrowest required lifetime**: singletons for stateful stores, transient for handlers and interceptors, scoped only when tied to an HTTP request
- Depend on interfaces, not concrete types — every class must receive its dependencies through the constructor via interfaces
- Never call `IServiceProvider.GetService<T>()` inside business logic (service locator anti-pattern); resolve all dependencies at construction time
- Use `IOptionsMonitor<T>` for options that must reflect runtime changes; use `IOptions<T>` only for options read once at startup

#### Error Handling

- Use `ArgumentNullException.ThrowIfNull(param)` and `ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value)` for parameter validation
- Never swallow exceptions with an empty `catch` block; log or rethrow
- Distinguish recoverable errors (return `null`, `Result<T>`) from programming errors (`throw`)
- `HttpTrafficRecord.Exception` stores `exception.ToString()` — never expose raw stack traces to external clients through the API response body

#### Performance

- Use `static readonly` for compiled `Regex` patterns (e.g., in `UrlPatternMatcher`) — or `[GeneratedRegex]` attribute for source-generated zero-allocation regex
- Prefer `Span<T>` / `Memory<T>` for byte manipulation (e.g., binary body Base64 encoding) over allocating intermediate arrays
- SQLite: use parameterised queries exclusively — never string-interpolate SQL
- Avoid `LINQ` in hot paths (e.g., `InMemoryTrafficStore.GetAll()` called on every poll); prefer `ConcurrentQueue<T>.ToArray()` directly

#### XML Documentation

- Every `public` and `internal` type and member must have a `<summary>` doc comment
- Parameters documented with `<param name="...">`, return values with `<returns>`, exceptions with `<exception cref="...">`
- Link related types with `<see cref="..."/>` where appropriate

---

### TypeScript Best Practices

Apply these to every new and modified TypeScript file in `dashboard-ui/src/`.

#### Type Safety

- **Strict mode** must be enabled in `tsconfig.json`: `"strict": true` (enables `strictNullChecks`, `noImplicitAny`, `strictFunctionTypes`, etc.)
- Never use `any` — use `unknown` when the type is genuinely unknown and narrow it with type guards
- Prefer `interface` for object shapes that are extended or implemented; prefer `type` for unions, intersections, and mapped types
- All function parameters and return types must be explicitly annotated unless inference is unambiguous (e.g., trivial arrow functions)
- Use `readonly` arrays (`ReadonlyArray<T>` or `readonly T[]`) for data passed to rendering functions that must not be mutated

#### Modern JavaScript / ECMAScript

- `const` by default; `let` only when reassignment is needed; never `var`
- Destructuring for parameter extraction and tuple unpacking
- Optional chaining (`?.`) and nullish coalescing (`??`) instead of verbose `if` guards
- `Array` methods (`map`, `filter`, `reduce`, `find`, `every`, `some`) instead of imperative `for` loops where intent is clearer
- Template literals for string interpolation; never `+` concatenation for multi-part strings
- Named exports over default exports — makes imports refactoring-safe and grep-friendly

#### Module & Component Design

- Each `.ts` file exports exactly one primary concern (class, set of related pure functions, or type definitions)
- No global mutable variables outside of the singleton `store.ts` — all shared state lives in `Store`
- Components depend on the `Store` interface, not on concrete implementations — this enables isolated testing with a fake store
- Event handlers attached in `init()` must be detachable (store the listener reference and call `removeEventListener` in `destroy()`)
- Canvas and SVG rendering functions must be pure: given the same data, produce the same output

#### Error Handling

- Wrap all `fetch` calls in `try/catch`; update `store.setConnectionStatus('disconnected')` on failure
- Never surface raw `Error.message` in the UI without sanitizing (use `escapeHtml()`)
- TypeScript `never` type in exhaustive `switch` statements to catch unhandled enum variants at compile time:
  ```typescript
  function assertNever(x: never): never {
    throw new Error(`Unhandled case: ${x}`);
  }
  ```

#### Performance

- Debounce filter input handlers (`hostInput`, `searchInput`) to avoid re-rendering on every keystroke
- For Canvas rendering: only redraw when the record list actually changes (compare length + last record id before calling `render()`)
- For SVG flow trees: use `DocumentFragment` to build the DOM off-screen before a single `appendChild`

---

### SOLID Principles

Every new class must be designed according to the five SOLID principles. The following explains how each principle applies concretely to the HttpLens codebase.

#### S — Single Responsibility Principle

> A class should have one reason to change.

- `HttpLensDelegatingHandler` has exactly one responsibility: intercept and record HTTP traffic. It delegates body reading to `BodyCapture`, header merging to `HeaderSnapshot`, masking to `SensitiveHeaderMasker`, and pattern filtering to `UrlPatternMatcher`. This decomposition must be maintained — do **not** inline logic from those helpers back into the handler.
- `SqliteTrafficStore` is responsible for persistence only — it must not contain analytics computation, export logic, or filtering logic.
- `GrpcLensInterceptor` is responsible for capturing gRPC calls only — gRPC-to-HTTP status code mapping belongs in a separate `GrpcStatusMapper` static class.
- `TrafficHubNotifier` has one job: listen to `ITrafficStore.OnRecordAdded` and push to SignalR clients. It must not contain store logic or filtering.
- TypeScript: `PollingService` fetches data; `Store` manages state; `TrafficTable` renders rows. They must never collapse into one class.

#### O — Open/Closed Principle

> Open for extension, closed for modification.

- `ITrafficStore` is the extension point for storage backends — add `SqliteTrafficStore` by implementing the interface, not by modifying `InMemoryTrafficStore` or the consumers.
- `TrafficFilter.Apply()` uses `TrafficFilterCriteria` — add new filter dimensions (e.g., `Protocol`) by extending the criteria record and adding one new `if` block inside `Apply()`, without touching any callers.
- Exporters (`CurlExporter`, `CSharpExporter`, `HarExporter`) are independent — add a new export format by creating a new static class, not by adding a switch inside an existing exporter.
- `BodyCapture` is extended with binary detection by adding `IsBinaryContentType()` as a new method, not by rewriting `CaptureAsync()` from scratch.

#### L — Liskov Substitution Principle

> Subtypes must be substitutable for their base types.

- `SqliteTrafficStore` must behave identically to `InMemoryTrafficStore` from the perspective of any class that depends on `ITrafficStore`:
  - `GetAll()` must return records in insertion order (newest first)
  - `OnRecordAdded` must fire synchronously after `Add()` completes
  - `Count` must equal the number of records returned by `GetAll()`
  - `Clear()` followed by `Count` must return 0
  - Violating any of these is an LSP violation
- `GrpcLensInterceptor` extends `Grpc.Core.Interceptors.Interceptor` — it must not change the observable behaviour of the calls it intercepts (same response returned to the caller, same exceptions propagated)

#### I — Interface Segregation Principle

> Clients should not be forced to depend on interfaces they do not use.

- `ITrafficStore` already segregates read and write concerns at the method level — do not add analytics computation methods to it; create a separate `ITrafficAnalytics` interface if needed.
- If the SignalR notifier needs to observe records being added but does not need to query them, it should depend only on an `ITrafficRecordSource` interface that exposes `OnRecordAdded` — not the full `ITrafficStore`.
- TypeScript: `TrafficTable` only needs `getFilteredRecords()` and `subscribe()` from the store — define a `IRecordListSource` interface and type the parameter to `TrafficTable` constructor against it in tests.

#### D — Dependency Inversion Principle

> Depend on abstractions, not concretions.

- All classes in `HttpLens.Core` and `HttpLens.Dashboard` must receive their dependencies through constructor parameters typed as interfaces or abstract base classes — never `new ConcreteClass()` inside a business-logic class.
- `HttpLensDelegatingHandler` receives `ITrafficStore`, not `InMemoryTrafficStore`.
- `TrafficHubNotifier` receives `ITrafficStore`, not `SqliteTrafficStore`.
- `GrpcLensInterceptor` receives `ITrafficStore` and `IOptionsMonitor<HttpLensOptions>` — not concrete implementations.
- TypeScript components receive the `Store` singleton through a module import; for testability, define a `IStore` interface type and pass it as a constructor parameter to `TrafficTable`, `DetailPanel`, and other components.

---

### Object-Oriented Principles

Beyond SOLID, the following OOP principles must be applied throughout.

#### Encapsulation

- Never expose mutable state directly. `InMemoryTrafficStore` exposes `IReadOnlyList<T>` snapshots, not the underlying `ConcurrentQueue<T>`.
- `HttpLensOptions` properties have public getters and setters (required by the options framework) — but internal state derived from options (e.g., compiled regex patterns in `UrlPatternMatcher`) must be private.
- TypeScript `Store` class has private `state` field — all state changes go through named mutation methods (`setRecords`, `setFilters`, etc.) that call `notify()`. Components never write `store.state.records = [...]`.
- SQLite connection strings and schema SQL must be `private const` strings inside `SqliteTrafficStore`, not exposed outside the class.

#### Abstraction

- Domain concepts are expressed as named types, not primitives:
  - `TrafficFilterCriteria` (not four separate nullable string parameters to `TrafficFilter.Apply()`)
  - `HttpTrafficRecord` (not a raw dictionary)
  - `ConnectionStatus` union type in TypeScript (not a magic string)
- `BodyCapture.CaptureAsync` hides the complexity of content buffering behind a simple return type of `(string? Body, long? SizeBytes)`
- New abstractions introduced for v1.5: `FlowNode`, `FlowGroup`, `AnalyticsResponse` — each represents a coherent domain concept, not an ad-hoc bag of data

#### Composition over Inheritance

- Prefer composing behaviours via constructor injection over subclassing. The existing codebase already follows this (e.g., `DiagnosticInterceptor` is composed into `DiagnosticInterceptorHostedService` rather than inheriting from it).
- `GrpcLensInterceptor` **must** extend `Grpc.Core.Interceptors.Interceptor` (framework requirement) — but any shared gRPC record-building logic should be extracted into a private helper class or static method, not pushed into the base class chain.
- TypeScript components do not inherit from each other — `TrafficTable` and `DetailPanel` are composed independently by `bootstrap()` in `index.ts`.

#### Immutability

- New model types should be immutable by default:
  - Use `init`-only setters or positional record syntax for model classes that represent captured data
  - `TrafficFilterCriteria` is already a positional `record` — follow this pattern for `FlowGroup`, `AnalyticsResponse`, `HostStats`, `TimelineBucket`
- In TypeScript, analytics and flow data objects returned from the API should be typed with `Readonly<T>` or `readonly` array properties to prevent accidental mutation in rendering code

#### Polymorphism

- The `ITrafficStore` interface enables transparent switching between `InMemoryTrafficStore` and `SqliteTrafficStore` — DI container injects the appropriate one based on config.
- Export logic is polymorphic through the three exporter classes (`CurlExporter`, `CSharpExporter`, `HarExporter`) — if a registry pattern is needed later, define an `ITrafficExporter` interface.
- In TypeScript, rendering functions for request/response bodies are polymorphic on content type: text → pretty-print JSON, image binary → `<img>`, non-image binary → hex dump. Implement this as a discriminated-union switch, not as a cascade of `instanceof` checks.

#### Separation of Concerns

- Keep presentation logic (HTML rendering) strictly separate from domain logic (record filtering, percentile computation)
- C#: `TrafficApiEndpoints` handles HTTP routing and serialization only — computation belongs in `TrafficFilter`, `TrafficAnalyticsService`, or `FlowTreeBuilder`
- TypeScript: `TrafficTable.renderRow()` formats data for display — it must not compute derived values like percentiles or lane assignments; those belong in `formatters.ts` or dedicated utility modules

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

### Engineering Principles Checklist

Before submitting any feature for review, verify all of the following. These are non-negotiable acceptance criteria derived from the [Engineering Standards & Principles](#engineering-standards--principles) section above.

#### Testing
- [ ] Every new C# class has a `{ClassName}Tests` file in the corresponding `*.Tests` project
- [ ] Every test method follows `{Method}_{Condition}_{ExpectedOutcome}` naming
- [ ] All tests are fully isolated — no real file I/O, no real HTTP calls, no `Thread.Sleep`
- [ ] `Moq` is used to mock all interface dependencies; concrete classes are never mocked
- [ ] Integration tests use `TestServer` / `WebApplicationFactory<T>` for Minimal API endpoints
- [ ] SQLite tests use `Data Source=:memory:` or a temp file cleaned up in `Dispose()`
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
- [ ] `SqliteTrafficStore` satisfies all `ITrafficStore` contracts (`GetAll` order, `Count`, `OnRecordAdded` timing) — substitutable for `InMemoryTrafficStore` (LSP)
- [ ] No interface has methods that some implementors don't need (ISP)
- [ ] All constructor parameters are typed as interfaces or abstractions, not concrete classes (DIP)

#### OOP
- [ ] No mutable public fields — all state changes go through methods
- [ ] Domain concepts expressed as named types, not primitives or magic strings
- [ ] Composition used instead of inheritance wherever the framework allows
- [ ] New model types use `init`-only setters or positional records for immutability
- [ ] Rendering/presentation logic is separated from domain/computation logic

---

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
