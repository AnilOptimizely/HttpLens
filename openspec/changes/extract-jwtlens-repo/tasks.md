# Tasks: Extract JwtLens into its own repository

## Phase 1: Lens.Abstractions publication
- [x] 1.1 Gate split: finalize `Lens.Abstractions` package metadata and publication process.
- [x] 1.2 Gate split: publish the shared contracts 0.1.0 for downstream consumption. Published as `HttpLens.Abstractions` 0.1.0 because the `Lens.` NuGet prefix is reserved by another owner. `JwtLens` 0.1.0 depends on the unpublishable `Lens.Abstractions` ID, so it was unlisted.
- [x] 1.3 Gate split: define the supported versioning and compatibility policy between `Lens.Abstractions` and `JwtLens`.

## Phase 2: Packaging and metadata cleanup
- [x] 2.1 Gate release: add a dedicated JwtLens README suitable for NuGet packaging.
- [x] 2.2 Gate release: wire the JwtLens README into package metadata.
- [x] 2.3 Gate release: remove the hardcoded contributor version from `/home/runner/work/HttpLens/HttpLens/src/JwtLens/JwtLensDiagnosticsContributor.cs`.
- [x] 2.4 Gate split: update JwtLens package metadata URLs for the future standalone repository.
- [x] 2.5 Gate split: audit inherited repo metadata so the stale `HttpClientStorybook` URLs in `/home/runner/work/HttpLens/HttpLens/Directory.Build.props:11-14` are not copied into the new repo.

## Phase 3: HttpLens compatibility suite
- [x] 3.1 Gate split: define the permanent compatibility suite scope in HttpLens.
- [x] 3.2 Gate split: make HttpLens the clear owner of the full compatibility suite.
- [x] 3.3 Gate split: recreate combined registration coverage against CI-built JwtLens packages for PR validation.
- [x] 3.4 Gate split: recreate shared handler pipeline coverage against CI-built JwtLens packages for PR validation.
- [x] 3.5 Gate split: recreate traffic capture compatibility coverage with JwtLens enabled.
- [x] 3.6 Gate split: recreate independent-store clearing coverage.
- [x] 3.7 Gate split: recreate dashboard traffic API compatibility coverage.
- [x] 3.8 Gate split: retire or replace the current `ProjectReference`-based `JwtLens.Regression.Tests` approach.

## Phase 4: New repository scaffolding, CI, and release
- [x] 4.1 Gate split: create a standalone JwtLens solution file.
- [x] 4.2 Gate split: create standalone `Directory.Build.props` with corrected metadata URLs.
- [x] 4.3 Gate split: create standalone `Directory.Packages.props`.
- [x] 4.4 Gate split: create standalone `nuget.config`.
- [x] 4.5 Gate split: add `global.json` to pin the SDK in the new JwtLens repo.
- [x] 4.6 Gate split: create JwtLens-only CI with no Node/dashboard build step.
- [x] 4.7 Gate split: create JwtLens-specific release automation and tagging strategy.
- [x] 4.8 Gate split: confirm NuGet ownership continuity for `PackageId=JwtLens`.

## Phase 5: History split
- [x] 5.1 Gate split: confirm the final `git filter-repo` path list.
- [x] 5.2 Gate split: include `/home/runner/work/HttpLens/HttpLens/docs/issues/design-jwtlens-v0.1.md` in extracted history.
- [x] 5.3 Gate split: execute the history-preserving split for `src/JwtLens` and `tests/JwtLens.Tests`.

## Phase 6: Post-split fixes in the new repo
- [x] 6.1 Gate split: replace the `Lens.Abstractions` project dependency with package consumption.
- [x] 6.2 Gate split: add product-local documentation and links back to family docs.
- [x] 6.3 Gate split: create the new minimal package-consumption sample.
- [x] 6.4 Gate release: verify package metadata, README wiring, and version reporting in the standalone repo.

## Phase 7: Post-split fixes in HttpLens
- [x] 7.1 Gate split: remove stale JwtLens project entries from the HttpLens solution.
- [x] 7.2 Gate split: convert or replace old JwtLens regression coverage in HttpLens.
- [x] 7.3 Gate split: update `/home/runner/work/HttpLens/HttpLens/README.md` to point to the standalone JwtLens repository.
- [x] 7.4 Gate split: update architecture documentation pointers so family docs remain canonical while JwtLens location changes.
- [ ] 7.5 Post-split improvement: add CI path filters to reduce unrelated monorepo build cost. Blocked: this happens only after the repository split.

## Phase 8: First standalone JwtLens preview release
- [x] 8.1 Gate release: keep `PackageId=JwtLens` and preserve existing NuGet ownership.
- [x] 8.2 Gate release: continue the existing version line (`JwtLens` 0.2.0-preview.1).
- [x] 8.3 Gate release: publish the first standalone JwtLens preview with JwtLens-specific tags/releases. Published from tag `v0.2.0-preview.1` in AnilOptimizely/JwtLens using NuGet Trusted Publishing.

## Phase 9: Follow-ups after the split
- [ ] 9.1 Follow-up: add JwtLens-side smoke tests against released HttpLens packages. Blocked: follow-up work after the split.
- [ ] 9.2 Follow-up: reconsider whether `Lens.Abstractions` eventually needs its own repository. Blocked: follow-up architectural decision after the split.
- [ ] 9.3 Follow-up: remove duplicated `TestJwtHelper` helpers if test structure changes justify it. Blocked: follow-up cleanup outside this scoped change.

## Review checklist
- [ ] Does the implementation match the task description and any PR feedback, with nothing extra added?
- [ ] Are the changes as small as possible for what they need to do?
- [ ] Do matching tests exist for every behavior that was changed or added?
- [ ] Are the XML doc comments accurate and spelled correctly?
- [ ] Is every comment needed? Remove comments that just repeat what the code says.
- [ ] If any SQL or data-access code is touched, can it be simplified or improved? (There is no SQL in JwtLens today, so this should usually be N/A.)
- [ ] Were any unnecessary interfaces, abstractions or boilerplate added? If so, remove them.
