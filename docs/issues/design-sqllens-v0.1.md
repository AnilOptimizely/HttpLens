# Design SqlLens v0.1

## Goal

Define the SqlLens v0.1 package architecture and scope for diagnosing SQL execution behavior in .NET data access stacks.

## Proposed v0.1 scope

- Define instrumentation targets for ADO.NET and common ORM query execution paths.
- Specify capture model for SQL command text metadata, duration, result status, and correlation IDs.
- Define parameter redaction policy and query normalization strategy.
- Define sampling and filtering controls to limit overhead and noise.
- Produce v0.1 package API and integration design aligned with Lens family standards.

## Out of scope

- Building complete provider-specific adapters for all databases.
- Query plan analysis and deep database performance tuning features.
- Full visualization dashboard implementation beyond initial data contract needs.

## Security/privacy considerations

- Redact or hash sensitive parameter values by default.
- Avoid storing full result sets or personally identifiable data.
- Document controls for data retention, local storage, and export.
- Ensure safe handling for connection strings and credentials in diagnostics output.

## Success criteria

- v0.1 design doc reviewed and approved.
- Minimum instrumentation targets and data model agreed.
- Security/privacy redaction defaults documented.
- Implementation milestones and backlog created.
