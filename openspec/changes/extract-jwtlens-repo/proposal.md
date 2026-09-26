# Proposal: Extract JwtLens into its own repository

## Why

`JwtLens` is already separated enough at the code boundary to live outside `/home/runner/work/HttpLens/HttpLens`, but the operational boundary is not ready yet.

The current codebase shows that `JwtLens` has its own registration and middleware surface, its own outbound handler, store, options, and analysis/model types under `/home/runner/work/HttpLens/HttpLens/src/JwtLens`, and only one production `ProjectReference`, to `Lens.Abstractions` (`/home/runner/work/HttpLens/HttpLens/src/JwtLens/JwtLens.csproj:20-21`). At the same time, nothing under `/home/runner/work/HttpLens/HttpLens/src/HttpLens*` references `JwtLens`, and `HttpLens.Core` keeps its own environment gating instead of using `EnvironmentGuard`, so it does not depend on `Lens.Abstractions` (`/home/runner/work/HttpLens/HttpLens/src/HttpLens.Core/Extensions/ServiceCollectionExtensions.cs:67-83`).

The blocking work is operational rather than architectural:

- build and package configuration is shared at the repo root (`/home/runner/work/HttpLens/HttpLens/HttpLens.slnx:2-20`, `/home/runner/work/HttpLens/HttpLens/Directory.Build.props:2-15`, `/home/runner/work/HttpLens/HttpLens/Directory.Packages.props:2-29`)
- CI always builds the dashboard frontend and whole solution, with no path filters (`/home/runner/work/HttpLens/HttpLens/.github/workflows/ci.yml:22-53`)
- release pushes every generated `.nupkg` on any `v*` tag (`/home/runner/work/HttpLens/HttpLens/.github/workflows/release.yml:35-39`)
- the current cross-package regression coverage is still source-coupled through `/home/runner/work/HttpLens/HttpLens/tests/JwtLens.Regression.Tests/JwtLens.Regression.Tests.csproj:17-20`
- `JwtLens` packaging is incomplete because it has no package README and hardcodes contributor version metadata in code (`/home/runner/work/HttpLens/HttpLens/src/JwtLens/JwtLens.csproj:2-22`, `/home/runner/work/HttpLens/HttpLens/src/JwtLens/JwtLensDiagnosticsContributor.cs:22-26`)

This proposal defines the planning work needed to split `JwtLens` cleanly while keeping package identity and compatibility intact.

## What changes

### Phase 1: establish shared-package boundaries
- publish and stabilize `Lens.Abstractions` as a first-class shared package
- switch the intended `JwtLens` dependency model from `ProjectReference` to `PackageReference`
- preserve `PackageId=JwtLens` and existing NuGet ownership continuity

### Phase 2: prepare packaging and metadata
- add a dedicated JwtLens README and wire it into package metadata
- remove hardcoded contributor version metadata and replace it with version metadata derived from the package or assembly
- correct package metadata URLs for the future standalone repo
- avoid carrying forward the stale `HttpClientStorybook` root metadata from `/home/runner/work/HttpLens/HttpLens/Directory.Build.props:7-14`

### Phase 3: replace source-coupled integration coverage
- retire the current `ProjectReference`-based `JwtLens.Regression.Tests` approach
- keep a small permanent compatibility suite in `HttpLens` that runs against released `JwtLens` packages
- cover coexistence, handler pipeline, traffic capture, independent stores, and dashboard traffic APIs

### Phase 4: scaffold the new JwtLens repository
- create standalone solution and repository-level build configuration
- create JwtLens-only CI and release workflows with no Node/dashboard build step
- create a minimal sample that consumes published packages instead of using monorepo project references

### Phase 5: perform the history-preserving split and repo fixups
- split history for `/home/runner/work/HttpLens/HttpLens/src/JwtLens` and `/home/runner/work/HttpLens/HttpLens/tests/JwtLens.Tests`
- optionally include `/home/runner/work/HttpLens/HttpLens/docs/issues/design-jwtlens-v0.1.md`
- exclude family-wide architecture and package-ID documents, which remain canonical in the monorepo
- update both repositories after the split to remove stale references and add cross-links

## Impact

### Affected in `AnilOptimizely/HttpLens`
- `/home/runner/work/HttpLens/HttpLens/HttpLens.slnx`
- `/home/runner/work/HttpLens/HttpLens/Directory.Build.props`
- `/home/runner/work/HttpLens/HttpLens/Directory.Packages.props`
- `/home/runner/work/HttpLens/HttpLens/.github/workflows/ci.yml`
- `/home/runner/work/HttpLens/HttpLens/.github/workflows/release.yml`
- `/home/runner/work/HttpLens/HttpLens/tests/JwtLens.Regression.Tests/`
- `/home/runner/work/HttpLens/HttpLens/README.md`
- `/home/runner/work/HttpLens/HttpLens/docs/lens-family-architecture.md`

### Affected in the future JwtLens repo
- extracted `/src/JwtLens`
- extracted `/tests/JwtLens.Tests`
- new repository scaffolding files (`.slnx`, `Directory.Build.props`, `Directory.Packages.props`, `nuget.config`, optional `global.json`)
- new JwtLens-only CI and release workflows
- new package README and product-local docs
- new minimal sample that consumes released packages

## Out of scope
- giving `Lens.Abstractions` its own repository
- implementing dashboard discovery/rendering of `ILensDiagnosticsContributor` data
- removing the duplicated `TestJwtHelper` helpers
- changing production code, project files, workflows, or the solution as part of this proposal-only change
- running `git filter-repo` or creating the new repository

## Risks
- version drift between `JwtLens` and `Lens.Abstractions`
- slower cross-repo changes when a feature spans shared contracts and JwtLens behavior
- the dashboard contributor integration still existing as future architecture rather than current production behavior (`/home/runner/work/HttpLens/HttpLens/docs/lens-family-architecture.md:132-141`, `/home/runner/work/HttpLens/HttpLens/docs/lens-family-architecture.md:208-217`)
- incomplete repo scaffolding leading to a split that preserves code but not packaging or release quality
