# Capability: jwtlens-packaging

## Requirement: JwtLens packaging SHALL include a package README
JwtLens MUST provide a dedicated package README and wire it into NuGet packaging before the first standalone release.

#### Scenario: first standalone release is prepared
- **WHEN** maintainers are ready to publish the first standalone JwtLens preview
- **THEN** the package MUST include a README configured through package metadata

## Requirement: JwtLens version metadata MUST NOT be hardcoded in the contributor
The version exposed by JwtLens contributor metadata MUST come from build or assembly metadata rather than a hardcoded source string.

#### Scenario: contributor metadata is reported
- **WHEN** JwtLens exposes package metadata at runtime
- **THEN** the version value SHALL be derived from built artifact metadata instead of a source literal

## Requirement: JwtLens package identity SHALL remain continuous
JwtLens MUST keep the existing package ID, NuGet ownership, and preview version line across the split.

#### Scenario: standalone package replaces monorepo-built package
- **WHEN** the new repository publishes JwtLens
- **THEN** it MUST keep `PackageId=JwtLens`
- **AND** it MUST preserve existing package ownership
- **AND** it MUST continue the existing preview version line
