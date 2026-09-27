# Capability: httplens-jwtlens-compatibility

## Requirement: HttpLens SHALL keep package-based compatibility coverage for JwtLens
HttpLens MUST keep a permanent compatibility suite that validates coexistence with JwtLens packages, using CI-built packages for pull request validation and released packages for ongoing compatibility validation.

#### Scenario: pull requests need unpublished integration validation
- **WHEN** HttpLens compatibility tests run for pull requests
- **THEN** the suite SHALL validate against a CI-built JwtLens package instead of waiting for a published release

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

## Requirement: JwtLens MAY add a small downstream smoke suite later
Any later JwtLens-side compatibility tests MUST remain a lightweight smoke layer rather than duplicating the full HttpLens-owned compatibility suite.

#### Scenario: JwtLens adds downstream smoke coverage
- **WHEN** JwtLens later adds tests against HttpLens packages
- **THEN** those tests SHALL remain smaller in scope than the primary HttpLens compatibility suite
