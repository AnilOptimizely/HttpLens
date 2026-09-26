# Tasks: Extract JwtLens into its own repository

## Phase 1: Lens.Abstractions publication
- [ ] 1.1 Gate split: finalize `Lens.Abstractions` package metadata and publication process.
- [ ] 1.2 Gate split: publish a supported `Lens.Abstractions` package version for downstream consumption.
- [ ] 1.3 Gate split: define the supported versioning and compatibility policy between `Lens.Abstractions` and `JwtLens`.

## Phase 2: Packaging and metadata cleanup
- [ ] 2.1 Gate release: add a dedicated JwtLens README suitable for NuGet packaging.
- [ ] 2.2 Gate release: wire the JwtLens README into package metadata.
- [ ] 2.3 Gate release: remove the hardcoded contributor version from `/home/runner/work/HttpLens/HttpLens/src/JwtLens/JwtLensDiagnosticsContributor.cs`.
- [ ] 2.4 Gate split: update JwtLens package metadata URLs for the future standalone repository.
- [ ] 2.5 Gate split: audit inherited repo metadata so the stale `HttpClientStorybook` URLs in `/home/runner/work/HttpLens/HttpLens/Directory.Build.props:11-14` are not copied into the new repo.

## Phase 3: HttpLens compatibility suite
- [ ] 3.1 Gate split: define the permanent compatibility suite scope in HttpLens.
- [ ] 3.2 Gate split: make HttpLens the clear owner of the full compatibility suite.
- [ ] 3.3 Gate split: recreate combined registration coverage against CI-built JwtLens packages for PR validation.
- [ ] 3.4 Gate split: recreate shared handler pipeline coverage against CI-built JwtLens packages for PR validation.
- [ ] 3.5 Gate split: recreate traffic capture compatibility coverage with JwtLens enabled.
- [ ] 3.6 Gate split: recreate independent-store clearing coverage.
- [ ] 3.7 Gate split: recreate dashboard traffic API compatibility coverage.
- [ ] 3.8 Gate split: retire or replace the current `ProjectReference`-based `JwtLens.Regression.Tests` approach.

## Phase 4: New repository scaffolding, CI, and release
- [ ] 4.1 Gate split: create a standalone JwtLens solution file.
- [ ] 4.2 Gate split: create standalone `Directory.Build.props` with corrected metadata URLs.
- [ ] 4.3 Gate split: create standalone `Directory.Packages.props`.
- [ ] 4.4 Gate split: create standalone `nuget.config`.
- [ ] 4.5 Gate split: add `global.json` to pin the SDK in the new JwtLens repo.
- [ ] 4.6 Gate split: create JwtLens-only CI with no Node/dashboard build step.
- [ ] 4.7 Gate split: create JwtLens-specific release automation and tagging strategy.
- [ ] 4.8 Gate split: confirm NuGet ownership continuity for `PackageId=JwtLens`.

## Phase 5: History split
- [ ] 5.1 Gate split: confirm the final `git filter-repo` path list.
- [ ] 5.2 Gate split: include `/home/runner/work/HttpLens/HttpLens/docs/issues/design-jwtlens-v0.1.md` in extracted history.
- [ ] 5.3 Gate split: execute the history-preserving split for `src/JwtLens` and `tests/JwtLens.Tests`.

## Phase 6: Post-split fixes in the new repo
- [ ] 6.1 Gate split: replace the `Lens.Abstractions` project dependency with package consumption.
- [ ] 6.2 Gate split: add product-local documentation and links back to family docs.
- [ ] 6.3 Gate split: create the new minimal package-consumption sample.
- [ ] 6.4 Gate release: verify package metadata, README wiring, and version reporting in the standalone repo.

## Phase 7: Post-split fixes in HttpLens
- [ ] 7.1 Gate split: remove stale JwtLens project entries from the HttpLens solution.
- [ ] 7.2 Gate split: convert or replace old JwtLens regression coverage in HttpLens.
- [ ] 7.3 Gate split: update `/home/runner/work/HttpLens/HttpLens/README.md` to point to the standalone JwtLens repository.
- [ ] 7.4 Gate split: update architecture documentation pointers so family docs remain canonical while JwtLens location changes.
- [ ] 7.5 Post-split improvement: add CI path filters to reduce unrelated monorepo build cost.

## Phase 8: First standalone JwtLens preview release
- [ ] 8.1 Gate release: keep `PackageId=JwtLens` and preserve existing NuGet ownership.
- [ ] 8.2 Gate release: continue the existing preview version line.
- [ ] 8.3 Gate release: publish the first standalone JwtLens preview with JwtLens-specific tags/releases.

## Phase 9: Follow-ups after the split
- [ ] 9.1 Follow-up: add JwtLens-side smoke tests against released HttpLens packages.
- [ ] 9.2 Follow-up: reconsider whether `Lens.Abstractions` eventually needs its own repository.
- [ ] 9.3 Follow-up: remove duplicated `TestJwtHelper` helpers if test structure changes justify it.

## Review checklist
- [ ] Does the implementation match the task description and any PR feedback, with nothing extra added?
- [ ] Are the changes as small as possible for what they need to do?
- [ ] Do matching tests exist for every behavior that was changed or added?
- [ ] Are the XML doc comments accurate and spelled correctly?
- [ ] Is every comment needed? Remove comments that just repeat what the code says.
- [ ] If any SQL or data-access code is touched, can it be simplified or improved? (There is no SQL in JwtLens today, so this should usually be N/A.)
- [ ] Were any unnecessary interfaces, abstractions or boilerplate added? If so, remove them.
