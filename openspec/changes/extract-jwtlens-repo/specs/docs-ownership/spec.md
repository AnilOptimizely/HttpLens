# Capability: docs-ownership

## Requirement: Family-wide Lens documentation SHALL remain canonical in the monorepo
Lens-family architecture and shared-contract documentation MUST stay in the HttpLens monorepo as the source of truth.

#### Scenario: family-wide docs are maintained after the split
- **WHEN** maintainers update Lens-family architecture, naming, or shared contract rules
- **THEN** those documents SHALL remain canonical in the monorepo

## Requirement: The standalone JwtLens repository SHALL contain only product-local docs
The standalone JwtLens repository MUST keep JwtLens-specific docs locally and MUST link back to family-wide docs maintained in the monorepo.

#### Scenario: maintainers add docs to the new JwtLens repo
- **WHEN** documentation is added for JwtLens after the split
- **THEN** product-specific usage, packaging, and sample docs SHALL live in the JwtLens repo
- **AND** family-wide architecture or shared contract rules SHALL be referenced via links to the monorepo
