# Lens Family Architecture

This document defines the shared architecture, conventions, and foundation for the **Lens family for .NET** — a cohesive set of focused diagnostics packages where each package targets one runtime domain.

## Goals

- Provide lightweight, zero-friction diagnostics packages for .NET developers.
- Each package focuses on one domain (HTTP, JWT, SQL, caching, etc.) and does one thing well.
- Share consistent conventions, configuration patterns, and safety defaults across all packages.
- Enable optional integration with the HttpLens dashboard for unified diagnostics visibility.
- Keep each package independently useful — no forced coupling between Lens packages.

## Non-goals

- Building a monolithic diagnostics framework.
- Replacing OpenTelemetry, Application Insights, or production APM tools.
- Providing production monitoring or alerting capabilities.
- Requiring all packages to be installed together.
- Supporting non-.NET runtimes.

## Package conventions

### Naming strategy

| Layer | Pattern | Example |
|-------|---------|---------|
| Domain package | `{Domain}Lens` | `JwtLens`, `SqlLens`, `CacheLens` |
| Core library (if split) | `{Domain}Lens.Core` | `JwtLens.Core` |
| Shared abstractions | `Lens.Abstractions` | — |
| Existing packages | `HttpLens`, `HttpLens.Core`, `HttpLens.Dashboard` | — |

All package IDs use PascalCase with no dots between domain and "Lens" (e.g., `JwtLens` not `Jwt.Lens`).

### Versioning strategy

- Each package is versioned independently using [SemVer 2.0](https://semver.org/).
- Pre-release versions use `-preview.N` suffix (e.g., `0.1.0-preview.1`).
- The shared `Lens.Abstractions` package uses conservative versioning — major bumps only for breaking contract changes.
- Placeholder packages already published at `0.0.1-placeholder` will start at `0.1.0` for first functional release.

### Public API naming conventions

- Registration extension methods: `Add{Package}(...)` on `IServiceCollection`.
- Middleware/pipeline methods: `Use{Package}()` on `IApplicationBuilder` (only when middleware is needed).
- Options classes: `{Package}Options` (e.g., `JwtLensOptions`).
- All public types use XML documentation comments.
- Interfaces prefixed with `I` per .NET convention.

## Configuration and options pattern

All Lens packages follow the same registration pattern:

```csharp
// Minimal registration
services.AddJwtLens();

// With options
services.AddJwtLens(options =>
{
    options.RedactClaims = true;
    options.AllowedEnvironments = ["Development"];
});

// Environment-aware registration (recommended)
services.AddJwtLens(environment, options =>
{
    options.SomeOption = value;
});

// Middleware (only if the package needs request pipeline access)
app.UseJwtLens();
```

### Options pattern rules

1. Every package has a single `{Package}Options` class as the primary configuration surface.
2. Options classes are always `sealed`.
3. All options have safe defaults (see Safety Defaults below).
4. Options are registered via `IOptions<T>` / `IOptionsMonitor<T>`.
5. The `Action<TOptions>?` configure parameter is always optional (nullable).

## Safety defaults

### Environment safety

All Lens packages are **development-only by default**:

- The `AllowedEnvironments` property defaults to `["Development"]`.
- In non-development environments, the package becomes a no-op unless explicitly opted in.
- The environment-aware `Add{Package}(IHostEnvironment, ...)` overload enforces this check at registration time.
- Packages must never throw in production if accidentally left registered — they silently disable.

### Redaction policy

All Lens packages **redact sensitive values by default**:

- Sensitive data (tokens, secrets, credentials, PII) is never captured in raw form by default.
- Each package defines domain-appropriate redaction rules (e.g., JwtLens redacts claim values, SqlLens redacts parameter values).
- Redaction is controlled via an `IRedactor` abstraction that packages can customise.
- There is **no** opt-in for raw value capture in v0.x — this may be considered in future versions with appropriate security review.
- Redacted values are replaced with a consistent placeholder: `[REDACTED]`.

### Safety summary table

| Default | Behaviour |
|---------|-----------|
| Environment | Development only |
| Redaction | Always on |
| Raw capture | Not supported in v0.x |
| Production | Silent no-op |
| Exceptions | Never thrown from diagnostics path |

## Dashboard integration

### Contract

Lens packages can optionally contribute diagnostics data to the HttpLens dashboard. Integration is:

- **Optional** — packages work standalone without the dashboard.
- **Loosely coupled** — via the `ILensDiagnosticsContributor` interface in `Lens.Abstractions`.
- **Additive** — each package contributes its own tab/section to the dashboard.

### Integration model

```
┌──────────────┐     ┌─────────────────────┐     ┌──────────────────┐
│   JwtLens    │────▶│  Lens.Abstractions  │◀────│  HttpLens.Dashboard│
│  (producer)  │     │  (contracts only)   │     │   (consumer)      │
└──────────────┘     └─────────────────────┘     └──────────────────┘
```

Each Lens package:
1. Implements `ILensDiagnosticsContributor` to describe what data it provides.
2. Registers a `LensDiagnosticsSnapshot` when capturing diagnostics events.
3. The dashboard discovers contributors via DI and renders their data.

### Future direction

- Dashboard tabs are contributed via metadata (package name, display name, icon hint).
- Snapshot data uses a simple key-value model initially, with structured models in later versions.
- Real-time updates via the existing SignalR infrastructure in HttpLens.Dashboard.

## Shared abstractions decision

### Decision: Add `Lens.Abstractions`

A minimal shared abstractions project is justified because:

1. **Redaction** — all packages need a common `IRedactor` interface to ensure consistent redaction behaviour.
2. **Environment safety** — the "development-only" check logic should be shared, not copy-pasted.
3. **Dashboard integration** — the contributor contract must be defined in a shared location that both producers (JwtLens, SqlLens) and the consumer (HttpLens.Dashboard) can reference.
4. **Package metadata** — a lightweight registration model helps the dashboard discover installed Lens packages.

### What goes in `Lens.Abstractions`

| Abstraction | Purpose |
|-------------|---------|
| `IRedactor` | Consistent value redaction across packages |
| `ILensDiagnosticsContributor` | Dashboard integration contract |
| `LensDiagnosticsSnapshot` | Simple diagnostics data model |
| `LensPackageMetadata` | Package self-description for discovery |
| `EnvironmentGuard` | Shared environment-safety helper |

### What does NOT go in `Lens.Abstractions`

- Domain-specific logic (JWT parsing, SQL interception, etc.)
- UI components or rendering logic
- Storage implementations
- Anything that requires dependencies beyond `Microsoft.Extensions.*`

## Project structure

```
src/
├── HttpLens/                    # Meta-package (existing)
├── HttpLens.Core/               # HTTP interception (existing)
├── HttpLens.Dashboard/          # Dashboard UI (existing)
├── Lens.Abstractions/           # NEW: shared contracts
│   ├── IRedactor.cs
│   ├── ILensDiagnosticsContributor.cs
│   ├── LensDiagnosticsSnapshot.cs
│   ├── LensPackageMetadata.cs
│   └── EnvironmentGuard.cs
└── JwtLens/                     # FUTURE: first domain package

tests/
├── HttpLens.Core.Tests/         # (existing)
├── HttpLens.Dashboard.Tests/    # (existing)
├── HttpLens.EndToEnd.Tests/     # (existing)
└── Lens.Abstractions.Tests/     # NEW: shared abstractions tests
```

## Guidance for building future packages

When creating a new Lens package (e.g., `JwtLens`):

1. **Create project** at `src/{PackageName}/{PackageName}.csproj`.
2. **Reference** `Lens.Abstractions` for shared contracts.
3. **Create options** class: `{PackageName}Options` following the pattern above.
4. **Create extension** method: `Add{PackageName}(...)` on `IServiceCollection`.
5. **Implement** `ILensDiagnosticsContributor` for dashboard integration.
6. **Default to safe**: development-only, redaction-on, no-throw.
7. **Add tests** at `tests/{PackageName}.Tests/`.
8. **Update** the solution file to include the new projects.
9. **Target** `net8.0;net9.0;net10.0` for multi-TFM support.
10. **Use** central package management (`Directory.Packages.props`).

## Follow-up tasks before JwtLens v0.1

1. ~~Define shared architecture~~ (this document)
2. ~~Create Lens.Abstractions skeleton~~ (this PR)
3. Design JwtLens v0.1 API surface (see `docs/issues/design-jwtlens-v0.1.md`)
4. Implement JwtLens v0.1 core interception
5. Add JwtLens dashboard tab integration
6. Update HttpLens.Dashboard to discover Lens contributors
7. Publish `Lens.Abstractions` v0.1.0-preview.1
8. Publish `JwtLens` v0.1.0-preview.1
