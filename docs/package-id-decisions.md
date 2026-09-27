# Package ID decisions for the Lens family for .NET

This decision record tracks reserved NuGet package IDs for the Lens family for .NET and the current publication status used to secure naming while package architecture is finalized.

## Reserved package IDs

| Package ID | Status |
|---|---|
| AiLens | Reserved; `0.0.1-placeholder` published |
| AsyncLens | Reserved; `0.0.1-placeholder` published |
| BlazorLens | Reserved; `0.0.1-placeholder` published |
| CacheLens | Reserved; `0.0.1-placeholder` published |
| ConfigLens | Reserved; `0.0.1-placeholder` published |
| CookieLens | Reserved; `0.0.1-placeholder` published |
| DiLens | Reserved; `0.0.1-placeholder` published |
| GcLens | Reserved; `0.0.1-placeholder` published |
| JwtLens | Reserved; `0.0.1-placeholder` published |
| LogLens | Reserved; `0.0.1-placeholder` published |
| MailLens | Reserved; `0.0.1-placeholder` published |
| MigrationLens | Reserved; `0.0.1-placeholder` published |
| SignalRLens | Reserved; `0.0.1-placeholder` published |
| SqlLens | Reserved; `0.0.1-placeholder` published |

## Shared infrastructure package

`Lens.Abstractions` uses the package ID `Lens.Abstractions` and is the supported shared contract package for Lens-family producers and consumers. Unlike the reserved product package IDs above, it is intended to be consumed as a real shared dependency rather than held as a placeholder-only reservation.

## Confirmation

All reserved package IDs listed above have placeholder packages published as version `0.0.1-placeholder`.

## Naming and branding direction

The naming direction is **The Lens family for .NET**: a cohesive set of focused diagnostics packages where each package targets one runtime domain and follows shared conventions.

## Lens.Abstractions publication and compatibility expectations

- While `Lens.Abstractions` remains in the HttpLens monorepo, keep `RepositoryUrl` pointed at `https://github.com/AnilOptimizely/HttpLens` and `PackageProjectUrl` pointed at `https://github.com/AnilOptimizely/HttpLens/tree/main/src/Lens.Abstractions`.
- Publish `Lens.Abstractions` through the existing tag-driven workflow in `/home/runner/work/HttpLens/HttpLens/.github/workflows/release.yml`.
- Treat `Lens.Abstractions` as a shared dependency package, not the main end-user entry point; user-facing applications should typically install packages such as `HttpLens` or `JwtLens` instead.
- Before JwtLens is extracted into its own repository, it must consume a published `Lens.Abstractions` package rather than a source `ProjectReference`.
- During the planned `0.1.x-preview` extraction phase, `JwtLens` and `Lens.Abstractions` should stay on compatible preview lines, starting with `0.1.0-preview.1`.
- Additive contract changes in `Lens.Abstractions` may ship within the current major version, but breaking contract changes require a new major version and coordinated downstream package updates.
