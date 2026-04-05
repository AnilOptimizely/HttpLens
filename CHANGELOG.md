# Changelog

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
