# Capability: lens-abstractions-package

## Requirement: Lens.Abstractions SHALL be treated as a first-class shared package
`Lens.Abstractions` MUST be published and maintained as a shared package boundary for Lens-family producers and consumers.

#### Scenario: JwtLens depends on a published shared contract
- **WHEN** JwtLens is prepared for repository extraction
- **THEN** it SHALL consume `Lens.Abstractions` through a package dependency rather than a source `ProjectReference`

#### Scenario: shared ownership remains outside JwtLens repo
- **WHEN** the JwtLens repository boundary is established
- **THEN** `Lens.Abstractions` MUST remain outside JwtLens repository ownership

## Requirement: Lens.Abstractions versioning MUST support JwtLens extraction
A supported versioning and compatibility policy MUST exist between `Lens.Abstractions` and `JwtLens` before the split is executed.

#### Scenario: package continuity is needed before the split
- **WHEN** JwtLens is cut over to package consumption
- **THEN** maintainers SHALL have a published `Lens.Abstractions` version suitable for the planned JwtLens preview line
