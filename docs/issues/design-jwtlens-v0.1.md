# Design JwtLens v0.1

## Goal

Define the JwtLens v0.1 package architecture and feature set for diagnosing JWT issuance and validation in .NET applications.

## Proposed v0.1 scope

- Define primary diagnostics surfaces for token issuance, parsing, and validation events.
- Specify core capture model for JWT metadata (issuer, audience, expiry, algorithm, key identifier, validation result).
- Define redaction defaults for sensitive claims and token payload handling.
- Define extension points for ASP.NET Core authentication/authorization pipeline integration.
- Produce v0.1 API design draft and package boundaries aligned with Lens family conventions.

## Out of scope

- Full production implementation of all diagnostics hooks.
- UI dashboard parity with HttpLens.
- Support for every identity provider or non-.NET runtime integration.

## Security/privacy considerations

- Never persist raw tokens by default.
- Enforce claim-level redaction/masking policies for sensitive data.
- Document secure defaults for storage retention and telemetry export behavior.
- Ensure diagnostics guidance avoids leaking secrets in logs or exception paths.

## Success criteria

- v0.1 design doc reviewed and accepted.
- Clear package API surface and extension points defined.
- Security and privacy defaults documented and approved.
- Implementation backlog created from agreed design.
