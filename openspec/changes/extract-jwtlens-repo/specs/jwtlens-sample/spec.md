# Capability: jwtlens-sample

## Requirement: The standalone JwtLens repository SHALL provide a minimal package-consumption sample
The standalone JwtLens repository MUST include a smaller sample that validates the public package-consumption path.

#### Scenario: sample is created for the standalone repo
- **WHEN** maintainers add a sample to the new JwtLens repository
- **THEN** it SHALL consume published packages rather than monorepo source project references

#### Scenario: existing monorepo sample is evaluated for extraction
- **WHEN** maintainers perform the history split
- **THEN** `/home/runner/work/HttpLens/HttpLens/samples/SampleJwtLensApi` MUST NOT be moved as-is
