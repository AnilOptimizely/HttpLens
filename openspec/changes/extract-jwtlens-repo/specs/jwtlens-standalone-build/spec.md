# Capability: jwtlens-standalone-build

## Requirement: The standalone JwtLens repository SHALL have independent build scaffolding
The standalone JwtLens repository MUST have its own solution file and root build configuration.

#### Scenario: new repo is scaffolded
- **WHEN** the standalone repository is created
- **THEN** it SHALL include its own solution file, `Directory.Build.props`, `Directory.Packages.props`, and `nuget.config`

#### Scenario: SDK pinning remains optional
- **WHEN** maintainers choose repository tooling policy
- **THEN** `global.json` MAY be added, but MUST NOT be required by this proposal

## Requirement: Standalone metadata MUST NOT inherit stale monorepo URLs
The standalone build configuration MUST use corrected JwtLens repository metadata and MUST NOT reuse the stale `HttpClientStorybook` URLs from `/home/runner/work/HttpLens/HttpLens/Directory.Build.props:11-14`.

#### Scenario: metadata is copied from the monorepo
- **WHEN** new root props are created for JwtLens
- **THEN** repository and package-project URLs MUST be reviewed and corrected for the standalone repo
