# Lens.Abstractions

Shared contracts for the Lens family for .NET.

> **Most application developers should install a product package such as [`HttpLens`](https://www.nuget.org/packages/HttpLens) or `JwtLens`, not `Lens.Abstractions` directly.** Install this package when you are building a Lens-family producer, consumer, or integration that needs the shared contract types.

## Installation

```shell
dotnet add package Lens.Abstractions
```

## What's Inside

| Type | Purpose |
|---|---|
| `ILensDiagnosticsContributor` | Shared contract for packages that publish diagnostics into a Lens dashboard or host |
| `LensDiagnosticsSnapshot` | Standard snapshot payload for package diagnostics |
| `LensPackageMetadata` | Shared package identity and display metadata |
| `IRedactor` / `DefaultRedactor` | Shared redaction abstraction and default implementation |
| `EnvironmentGuard` | Shared environment matching helper |

## Compatibility

- `Lens.Abstractions` is the shared package boundary for Lens-family producers and consumers.
- While it remains in the HttpLens monorepo, package metadata and releases follow the same tag-based workflow as the other published packages.
- Contract changes are versioned conservatively: additive changes stay within the current major version, while breaking contract changes require a major version bump and coordinated downstream updates.

For family-wide architecture and package conventions, see the [Lens family architecture](https://github.com/AnilOptimizely/HttpLens/blob/main/docs/lens-family-architecture.md).
