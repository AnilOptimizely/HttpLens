# Capability: jwtlens-ci-release

## Requirement: JwtLens SHALL have repository-local CI
The standalone JwtLens repository MUST have CI that builds and tests JwtLens without requiring the HttpLens dashboard frontend build.

#### Scenario: JwtLens CI executes
- **WHEN** the standalone repository CI runs
- **THEN** it MUST NOT require the Node-based dashboard build steps used by `/home/runner/work/HttpLens/HttpLens/.github/workflows/ci.yml:22-39`

## Requirement: JwtLens SHALL have package-specific release automation
The standalone JwtLens repository MUST release JwtLens using package-specific tags and workflows.

#### Scenario: a release is cut from the standalone repo
- **WHEN** maintainers publish a new JwtLens preview
- **THEN** the workflow MUST publish JwtLens artifacts without pushing unrelated packages

#### Scenario: package-specific tag strategy is used
- **WHEN** release automation is configured
- **THEN** JwtLens MUST use its own tag/release strategy instead of the monorepo `v*` behavior that pushes every `.nupkg`
