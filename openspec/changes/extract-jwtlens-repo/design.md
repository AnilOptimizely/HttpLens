# Design: Extract JwtLens into its own repository

## Overview

This change defines the target operating model for moving `/home/runner/work/HttpLens/HttpLens/src/JwtLens` out of the `AnilOptimizely/HttpLens` monorepo and into a standalone `JwtLens` repository without changing production code in this proposal phase.

The current state already has a workable code boundary. The target state adds the missing operational boundary: package dependencies, repo-local build configuration, repo-local CI and release automation, package-based compatibility coverage, and clear ownership of family-wide versus product-local docs.

## Current state

### Code boundary
- `JwtLens` is a standalone package with its own registration and runtime surface under `/home/runner/work/HttpLens/HttpLens/src/JwtLens`.
- `JwtLens` has exactly one production project dependency, on `Lens.Abstractions` (`/home/runner/work/HttpLens/HttpLens/src/JwtLens/JwtLens.csproj:20-21`).
- `HttpLens`, `HttpLens.Core`, and `HttpLens.Dashboard` do not reference `JwtLens` back (`/home/runner/work/HttpLens/HttpLens/src/HttpLens/HttpLens.csproj:16-20`, `/home/runner/work/HttpLens/HttpLens/src/HttpLens.Core/HttpLens.Core.csproj:18-31`, `/home/runner/work/HttpLens/HttpLens/src/HttpLens.Dashboard/HttpLens.Dashboard.csproj:18-30`).
- `HttpLens.Core` duplicates environment gating instead of consuming `EnvironmentGuard`, so it remains independent of `Lens.Abstractions` (`/home/runner/work/HttpLens/HttpLens/src/HttpLens.Core/Extensions/ServiceCollectionExtensions.cs:67-83`).

### Build boundary
- The monorepo uses one solution and root-level shared props/package versions (`/home/runner/work/HttpLens/HttpLens/HttpLens.slnx:2-20`, `/home/runner/work/HttpLens/HttpLens/Directory.Build.props:2-15`, `/home/runner/work/HttpLens/HttpLens/Directory.Packages.props:2-29`).
- `/home/runner/work/HttpLens/HttpLens/Directory.Build.props:11-14` still points at `HttpClientStorybook` repository URLs, so it is not safe to copy into a new repo unchanged.
- No `global.json`, `Directory.Build.targets`, or `.editorconfig` currently exist at the repo root.

### CI and release boundary
- CI always installs Node, type-checks/builds the dashboard UI, then restores, builds, and tests the whole repository (`/home/runner/work/HttpLens/HttpLens/.github/workflows/ci.yml:22-53`).
- The current workflows do not use `paths:` filters.
- Release is monorepo-wide and pushes all generated packages for any `v*` tag (`/home/runner/work/HttpLens/HttpLens/.github/workflows/release.yml:3-39`).

### Test boundary
- `/home/runner/work/HttpLens/HttpLens/tests/JwtLens.Tests/JwtLens.Tests.csproj:17-18` references only `JwtLens`.
- `/home/runner/work/HttpLens/HttpLens/tests/JwtLens.Regression.Tests/JwtLens.Regression.Tests.csproj:17-20` references `HttpLens.Core`, `HttpLens.Dashboard`, `JwtLens`, and `Lens.Abstractions`.
- Compatibility behaviors already covered in the regression suite include shared registration, handler coexistence, traffic capture, independent stores, and dashboard traffic API behavior (`/home/runner/work/HttpLens/HttpLens/tests/JwtLens.Regression.Tests/CombinedRegistrationTests.cs:110-135`, `/home/runner/work/HttpLens/HttpLens/tests/JwtLens.Regression.Tests/TrafficInterceptionRegressionTests.cs:171-233`, `/home/runner/work/HttpLens/HttpLens/tests/JwtLens.Regression.Tests/DashboardApiRegressionTests.cs:85-221`, `/home/runner/work/HttpLens/HttpLens/tests/JwtLens.Regression.Tests/IsolationAndIndependenceTests.cs:86-199`).

### Packaging and docs boundary
- `JwtLens` and `Lens.Abstractions` are both versioned `0.1.0-preview.1`, while `HttpLens.*` packages are `1.3.0.0` (`/home/runner/work/HttpLens/HttpLens/src/JwtLens/JwtLens.csproj:11-15`, `/home/runner/work/HttpLens/HttpLens/src/Lens.Abstractions/Lens.Abstractions.csproj:11-15`, `/home/runner/work/HttpLens/HttpLens/src/HttpLens/HttpLens.csproj:10-14`).
- `JwtLens` has no `PackageReadmeFile`, unlike the `HttpLens` packages (`/home/runner/work/HttpLens/HttpLens/src/JwtLens/JwtLens.csproj:2-22`, `/home/runner/work/HttpLens/HttpLens/src/HttpLens/HttpLens.csproj:4-20`, `/home/runner/work/HttpLens/HttpLens/src/HttpLens.Core/HttpLens.Core.csproj:5-31`, `/home/runner/work/HttpLens/HttpLens/src/HttpLens.Dashboard/HttpLens.Dashboard.csproj:5-25`).
- `JwtLensDiagnosticsContributor` hardcodes `Version = "0.1.0-preview.1"` in source (`/home/runner/work/HttpLens/HttpLens/src/JwtLens/JwtLensDiagnosticsContributor.cs:22-26`).
- Family-wide documentation is currently centered in the monorepo (`/home/runner/work/HttpLens/HttpLens/docs/lens-family-architecture.md:113-217`, `/home/runner/work/HttpLens/HttpLens/docs/package-id-decisions.md:1-30`).

## Target state

### Dependency map

| Area | Current state | Target state |
|---|---|---|
| Code | `JwtLens` references `Lens.Abstractions` by project reference | `JwtLens` consumes published `Lens.Abstractions` via `PackageReference` |
| Build | Shared monorepo solution and root props/packages | Standalone JwtLens repo with its own solution and root config |
| CI | Monorepo CI always builds dashboard UI and full solution | JwtLens-only CI with no Node/dashboard step |
| Release | Monorepo `v*` tags push all `.nupkg` files | JwtLens-specific tags/releases push only JwtLens artifacts |
| Tests | Cross-package compatibility lives in source-coupled regression project | HttpLens keeps a package-based compatibility suite against released JwtLens |
| Sample | SampleJwtLensApi uses monorepo project reference | New minimal sample consumes published packages |
| Docs | Family and product docs mixed in monorepo | Family docs remain in monorepo; JwtLens repo contains product-local docs and links back |

## Maintainer decisions

### Decision 1: `Lens.Abstractions` stays separate and becomes a first-class shared package
**Selected:** Publish and stabilize `Lens.Abstractions`, then have `JwtLens` consume it via `PackageReference`.

**Why selected:**
- The architecture explicitly defines `Lens.Abstractions` as the shared contract for producers and consumers across the Lens family (`/home/runner/work/HttpLens/HttpLens/docs/lens-family-architecture.md:117-162`).
- It keeps shared contracts out of a JwtLens-specific ownership boundary.

**Rejected alternatives:**
- **Move `Lens.Abstractions` into the new JwtLens repo**: rejected because it would make a family-wide contract appear product-owned.
- **Give `Lens.Abstractions` its own repo now**: rejected because it adds more operational overhead before multiple split packages justify it.

### Decision 2: HttpLens keeps the permanent compatibility suite
**Selected:** Replace source-coupled regression coverage with a smaller package-based compatibility suite in `HttpLens`.

**Why selected:**
- The main integration risk is preserving HttpLens behavior when JwtLens is installed.
- Existing regression tests already validate the right compatibility cases.

**Rejected alternatives:**
- **Move the entire regression suite into the new JwtLens repo**: rejected because the behaviors being protected are mostly HttpLens-facing.
- **Drop compatibility testing entirely**: rejected because it would remove the only automated cross-repo coexistence signal.

### Decision 3: create a new smaller sample instead of moving `SampleJwtLensApi`
**Selected:** Leave `/home/runner/work/HttpLens/HttpLens/samples/SampleJwtLensApi` out of the history split and create a fresh, smaller sample in the new repo.

**Why selected:**
- The current sample uses a project reference and monorepo assumptions (`/home/runner/work/HttpLens/HttpLens/samples/SampleJwtLensApi/SampleJwtLensApi.csproj:5-7`).
- The replacement sample should validate the public consumer experience.

**Rejected alternatives:**
- **Move the sample unchanged**: rejected because it would import broken project-reference assumptions.
- **Carry the sample history and then rewrite it immediately**: rejected because the rewrite is more important than the sample’s old file history.

### Decision 4: block the first standalone release on packaging cleanup
**Selected:** Do not ship the first standalone JwtLens release until README packaging and version metadata cleanup are complete.

**Why selected:**
- The package should have a proper NuGet landing document.
- Version metadata must not be hardcoded in the contributor implementation.

**Rejected alternatives:**
- **Release first and clean up later**: rejected because package metadata debt becomes public API/support debt immediately.

### Decision 5: keep family-wide docs in the monorepo
**Selected:** Leave family architecture, contract rules, and naming/versioning conventions in the monorepo; keep only JwtLens-specific docs in the new repo.

**Why selected:**
- Family-wide docs span more than one package and would drift if duplicated.
- The monorepo remains the canonical place for Lens family design.

**Rejected alternatives:**
- **Copy shared docs into JwtLens**: rejected because duplicated architecture documents would drift.
- **Move all family docs to JwtLens**: rejected because JwtLens does not own the whole Lens family.

## History-preserving split plan

### `git filter-repo` path list
Required paths:
- `src/JwtLens`
- `tests/JwtLens.Tests`

Optional path:
- `docs/issues/design-jwtlens-v0.1.md`

Excluded paths:
- `samples/SampleJwtLensApi`
- `docs/lens-family-architecture.md`
- `docs/package-id-decisions.md`

### Inclusion rationale
- `src/JwtLens` is the production package being extracted.
- `tests/JwtLens.Tests` is the self-contained unit test project that belongs with the package.
- `docs/issues/design-jwtlens-v0.1.md` may be included if preserving package-local design history is desirable.

### Exclusion rationale
- `samples/SampleJwtLensApi` is excluded because the new repo will intentionally replace it with a smaller package-consumption sample.
- `docs/lens-family-architecture.md` and `docs/package-id-decisions.md` are excluded because they are family-wide canonical docs that stay in the monorepo.

## Version handling after removing the hardcoded contributor version

After extraction, `JwtLensDiagnosticsContributor` should obtain package/version metadata from assembly or package metadata instead of embedding a string literal in source. An acceptable design is:

- derive the version from assembly informational or file version metadata at runtime
- keep the contributor metadata populated from the built artifact rather than a manually synchronized string
- ensure package and contributor metadata stay aligned across preview releases

This preserves the current preview line while removing source-level release coupling.

## Migration sequence

1. stabilize and publish `Lens.Abstractions`
2. switch `JwtLens` to package consumption of `Lens.Abstractions`
3. build the package-based compatibility suite in `HttpLens`
4. clean up packaging and docs (README, version metadata, URLs)
5. create the new repository scaffolding
6. split history using the approved path list
7. fix up both repositories after the split
8. cut the first standalone JwtLens preview release

## Open questions

- Should the optional design doc history (`docs/issues/design-jwtlens-v0.1.md`) be retained in the new repo, or referenced only from the monorepo?
- Should the new JwtLens repo pin an SDK with `global.json`, or inherit the latest supported SDK policy?
- Should the remaining HttpLens repo convert compatibility tests to consume a fixed JwtLens version, a floating preview line, or a package built in CI for pull requests?
- When the dashboard eventually consumes contributor data in production, should compatibility coverage remain only in HttpLens or be mirrored with JwtLens-side smoke tests?
