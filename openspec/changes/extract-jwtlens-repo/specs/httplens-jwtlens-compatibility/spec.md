# Capability: httplens-jwtlens-compatibility

## Requirement: HttpLens SHALL keep package-based compatibility coverage for JwtLens
HttpLens MUST keep a permanent compatibility suite that validates coexistence with released JwtLens packages.

#### Scenario: both packages register together
- **WHEN** HttpLens compatibility tests run against a released JwtLens package
- **THEN** the suite SHALL verify that both packages can be registered in one application

#### Scenario: both handlers work in one pipeline
- **WHEN** outbound HTTP requests flow through HttpClientFactory
- **THEN** the suite SHALL verify that HttpLens and JwtLens handlers both operate correctly in the same pipeline

#### Scenario: HttpLens traffic capture still works with JwtLens enabled
- **WHEN** JwtLens is enabled in the same host as HttpLens
- **THEN** the suite SHALL verify that HttpLens traffic capture still functions correctly

#### Scenario: clearing one package store does not affect the other
- **WHEN** either package store is cleared during compatibility testing
- **THEN** the suite SHALL verify that the other package store remains unaffected

#### Scenario: dashboard traffic APIs behave correctly with JwtLens present
- **WHEN** HttpLens dashboard traffic endpoints are exercised with JwtLens installed
- **THEN** the suite SHALL verify that traffic APIs still return HttpLens traffic behavior rather than JwtLens-specific data
