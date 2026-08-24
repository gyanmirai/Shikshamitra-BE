# ADR-006: Effective-Dated Statutory Payroll Engine

## Status

Accepted.

## Context

Statutory payroll obligations cannot be represented safely as ordinary fixed or percentage deductions.
ESI applicability depends on establishment coverage, a legal wage classification, employee enrollment history, wage ceilings, contribution-period continuation, effective dates, and separate employee and employer contributions.
Salary templates are reusable onboarding helpers and must not become the legal source of truth.

## Decision

Use a generic statutory payroll boundary with platform-managed effective-dated rule versions and tenant-managed establishment, wage-treatment, enrollment, and override records.
Implement ESI as the first scheme for jurisdiction `IN-GJ`.
Run the ESI calculator automatically during payroll whenever the tenant establishment is active.
Treat salary-template display as explanatory only and prevent template removal from bypassing the statutory calculation.
Require a tenant-wide impact preview with no blocking readiness issues before activation.
Store statutory rule and employee decision metadata in the payroll snapshot and immutable salary slip.

## Consequences

New statutory schemes can reuse the rule, establishment, enrollment, treatment, override, preview, and snapshot patterns.
Payroll remains deterministic for a locked month because the exact effective rule and decision metadata are retained.
Tenant administrators must classify every active earning component and resolve employee readiness before payroll can lock.
Changes to active establishment configuration return the scheme to review status and require a fresh activation preview.
