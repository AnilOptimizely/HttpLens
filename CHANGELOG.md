# Changelog

## [1.2.0] — Filtering & URL Patterns

### Added

- `ExcludeUrlPatterns` option on `HttpLensOptions` — glob-style patterns (`*` wildcard) to exclude specific URLs from capture. Any matching URL is skipped. Exclude takes precedence over include.
- `IncludeUrlPatterns` option on `HttpLensOptions` — glob-style patterns to capture only matching URLs. When non-empty, only URLs matching at least one pattern are captured.
- `UrlPatternMatcher` — static utility class that evaluates URL capture decisions based on exclude/include patterns. Integrated into both `HttpLensDelegatingHandler` and `DiagnosticInterceptor`.
- `TrafficFilterCriteria` — immutable record for server-side traffic filtering criteria (method, status, host, search).
- `TrafficFilter` — static class that applies server-side filtering to traffic records. All criteria combined with AND logic.
- Server-side filtering on `GET /api/traffic` — new optional query parameters: `method`, `status`, `host`, `search`. Filters applied before pagination; `total` reflects filtered count.
- Dashboard filter bar — visual filter bar with method dropdown, status dropdown, host input, search input, and clear button. Wired to the client-side store for instant filtering.
- `FilterBar` TypeScript component — initializes filter inputs and syncs state with the store.
- Comprehensive test suite: `UrlPatternMatcherTests` (13 tests), handler integration tests (4 tests), `TrafficFilterTests` (12 tests), API integration tests (7 tests).

### Changed

- `HttpLensDelegatingHandler.SendAsync()` now checks URL patterns before creating a traffic record.
- `DiagnosticInterceptor.HandleStart()` now checks URL patterns before creating a traffic record.
- `TrafficApiEndpoints.MapHttpLensApi()` GET /traffic endpoint now accepts filter query parameters.
- Dashboard `index.html` now includes a filter bar between the header and the main layout.

## [2.0.0] — Security & Authorization

### Added

- `IsEnabled` master switch on `HttpLensOptions` — disables all capture and returns 404 on dashboard when `false`
- `AllowedEnvironments` option — skip service registration entirely when the current hosting environment is not in the list (zero overhead)
- `ApiKey` option — require `X-HttpLens-Key` header or `?key=` query parameter to access dashboard and API
- `AuthorizationPolicy` option — apply any named ASP.NET Core authorization policy to all HttpLens routes
- `AllowedIpRanges` option — restrict dashboard access by IPv4, IPv6, or CIDR range (e.g. `10.0.0.0/8`)
- `AddHttpLens(IHostEnvironment, Action<HttpLensOptions>?)` overload — checks `AllowedEnvironments` at registration time
- `EnabledGuardMiddleware` — path-scoped 404 when `IsEnabled = false`
- `IpAllowlistMiddleware` — CIDR matching with IPv4-mapped IPv6 normalisation (`::ffff:127.0.0.1` → `127.0.0.1`)
- `ApiKeyAuthMiddleware` — header-first, query-param fallback; returns 401 with JSON error body
- Frontend API key support — `traffic-api.service.ts` reads `?key=` from page URL, stores in `sessionStorage`, injects `X-HttpLens-Key` on every fetch; 401 responses show clear message
- Comprehensive test suite: 135 automated tests covering all security layers, layered combinations, middleware execution order, and edge cases

### Changed

- `HttpLensDelegatingHandler` migrated from `IOptions<HttpLensOptions>` to `IOptionsMonitor<HttpLensOptions>` — options are now read at request time, enabling runtime toggles without restart
- `DiagnosticInterceptor` migrated from `IOptions<HttpLensOptions>` to `IOptionsMonitor<HttpLensOptions>`
- `MapHttpLensApi` return type changed from `void` to `RouteGroupBuilder` (backward-compatible)
- Security middleware applied automatically inside `MapHttpLensDashboard()` — no `UseMiddleware` calls needed in host apps
- All security layers are opt-in — existing users with no security config see zero behavior change

## [1.1.0] — DiagnosticListener Interception

### Added
- `DiagnosticInterceptor` — process-wide interception via `System.Diagnostics.DiagnosticListener` that captures outbound HTTP traffic from manually-newed `HttpClient` instances
- `DiagnosticInterceptorHostedService` — `IHostedService` that manages the interceptor lifecycle
- `EnableDiagnosticInterception` option on `HttpLensOptions` (default: `true`)
- Deduplication flag (`HttpLens.CapturedByHandler`) on `HttpRequestMessage.Options` to prevent double-capture
- Manual HttpClient records show `HttpClientName = "(manual)"` in the dashboard
- `/api/manual` test endpoint in SampleWebApi
- Comprehensive xUnit tests for DiagnosticInterceptor

### Changed
- `HttpLensDelegatingHandler` now sets `HttpLens.CapturedByHandler` option on requests before invoking the inner handler
- `ServiceCollectionExtensions.AddHttpLens()` now registers `DiagnosticInterceptor` and its hosted service

## [1.0.0] — Production Ready

### Added
- Sensitive header masking (Authorization, Cookie, Set-Cookie, X-Api-Key) — headers masked before storage
- Custom sensitive headers configurable via `HttpLensOptions.SensitiveHeaders`
- `RetryDetectionHandler` — Polly retry detection and grouping
- `AddRetryDetection()` extension method for `IHttpClientBuilder`
- Export as cURL — one-click copy of valid cURL command
- Export as C# — one-click copy of `HttpClient`/`HttpRequestMessage` code
- Export as HAR 1.2 — download traffic as HAR file, importable in Chrome DevTools
- Dark/light theme toggle with localStorage persistence
- Correlation tab — TraceId, ParentSpanId, InboundRequestPath, HttpClient name, retry group info
- Export tab — cURL, C#, and HAR export in the detail panel
- Retry group visual — retried requests grouped and indented in the traffic table
- API endpoints for retry groups and exports
- `GetByRetryGroupId()` on `ITrafficStore`
- `SampleWithPolly` sample project demonstrating retry grouping
- Comprehensive test coverage for masker, exporters, and retry handler

### Changed
- `HttpLensDelegatingHandler` now masks sensitive headers and reads retry context
- Version bumped to 1.0.0 across all packages

## [0.5.0] — Dashboard & Live Updates

### Added
- Embedded TypeScript SPA dashboard with esbuild bundling
- REST API with JSON endpoints for traffic data
- Polling-based live updates
- Inbound-to-outbound request correlation via Activity tags
- Client-side filtering (method, status, host, text search)
- Connection status indicator
- Options validation

## [0.1.0] — Initial Release

### Added
- `HttpLensDelegatingHandler` — captures all outbound `HttpClient` requests/responses
- `InMemoryTrafficStore` — thread-safe ring-buffer in-memory traffic store
- `AddHttpLens()` extension — registers services and auto-attaches to all `IHttpClientFactory` clients
- `MapHttpLensDashboard()` extension — mounts the embedded SPA + JSON API
- Traffic JSON API: `GET /api/traffic`, `GET /api/traffic/{id}`, `DELETE /api/traffic`
- TypeScript dashboard UI with dark theme (esbuild-bundled, embedded resource)
- Sample `SampleWebApi` project
- xUnit test suite for Core and Dashboard
- GitHub Actions CI/CD workflows
