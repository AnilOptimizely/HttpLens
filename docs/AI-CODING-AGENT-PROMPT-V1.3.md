# Prompt for AI Coding Agent: Build HttpLens v1.3 Features

> **Standalone v1.3 milestone prompt only — "Real-Time & Persistence".**

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
| SignalR (F1) | `TrafficHubNotifier` broadcasts on event; hub security filter returns 401/403/404 | `SignalRService` connection lifecycle; fallback triggers polling |
| SQLite (F2) | All `ITrafficStore` operations; ring-buffer eviction; thread-safety; schema creation | n/a |
| Connection UI (F3) | — | `ConnectionIndicator` renders correct icon per state; store `setConnectionMode` |

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

### Documentation
- Update `CHANGELOG.md` with `[1.3.0]` section
- Update `README.md` with new configuration options, API endpoints, feature descriptions

### Security
- All new API endpoints go through existing security endpoint filter in `MapHttpLensApi`
- New dependencies: check for known security advisories before adding
- SignalR hub must respect all security layers

### Dashboard Views
- For v1.3, dashboard views include Table and live-connection UX enhancements for real-time updates
- Any added connection indicators must support both dark and light themes

### Backward Compatibility
- SQLite persistence is opt-in (default off)
- SignalR connection falls back to polling
- All existing tests must continue to pass

### Recommended Implementation Order
1. **SQLite Persistence** (Feature 2)
2. **SignalR Real-Time Push** (Feature 1)
3. **Enhanced Dashboard Connection UI** (Feature 3)
