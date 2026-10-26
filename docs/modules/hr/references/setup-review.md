---
module: hr
domain: setup-review
content_type: Reference
status: canonical
updated_on: 2026-10-26
owner: backend-team
audience: humans-and-agents
---

# Advisory school HR setup review

Review evidence distinguishes available defaults from settings deliberately reviewed for the school.
It does not gate staff activation, leave requests or payroll, and never changes policies or entitlements.

## Additive API

GET `/api/hr/setup-review/{academicYearId}/{section}` reads a section. POST to the same URL records its review.
Sections are exactly `calendar`, `attendance`, `leave`, `payroll`, `sample`.

| Section | Read action | Record action, also requiring Read |
| --- | --- | --- |
| Calendar, attendance, payroll | `HR.Policies.View` | `HR.Policies.Edit` |
| Leave | `HR.LeaveManagement.Setting.LeavePolicy.View` | `HR.LeaveManagement.Setting.LeavePolicy.Edit` |
| Sample | `HR.Policies.View` and `HR.LeaveManagement.Setting.LeavePolicy.View` | `HR.Policies.PreviewImpact` |

No new permissions, grants, roles or visibility settings are created. Application services enforce these checks directly.
Every request requires an active current-tenant identity and a current-tenant year; host requests are rejected.

GET accepts optional `employeeId` and `date` for the sample, with date inside the requested year.
Without parameters it reopens the recorded sample, or chooses the first authorized staff ID and year start date.
`newSample=true` deliberately selects a new sample instead of reopening old evidence; this read never records completion.
The sample uses `HrPolicyResolver`, tenant and academic-year staff scope. Explicit out-of-scope staff is rejected.
An inaccessible recorded sample becomes stale/deferred without silently substituting another staff member.

POST accepts `{ fingerprint, concurrencyStamp, employeeId?, date? }`. Actor, tenant and reviewed time are server derived;
unknown JSON actor/time/tenant fields cannot override them. Missing/invalid settings, no staff and deferred checks cannot be recorded complete.

| Response | Meaning |
| --- | --- |
| `configurationState` | `valid`, `missing`, `invalid`, `no_staff`, `deferred` |
| `reviewState` | `record_not_present`, `reviewed`, `stale` |
| `starterInitialized` | Existing year `HrStarterPoliciesInitialized` marker only |
| `tenantStarterEnabled` | Existing tenant starter opt-in, independently reported |
| `fingerprint`, `concurrencyStamp` | Current section hash and year optimistic concurrency token |
| `reviewedBy`, `reviewedByName`, `reviewedAt` | Persisted actor ID, current-tenant display name and server UTC time |
| `details`, `notices`, `employeeId`, `date`, `canReview` | Summary, cautions, selected sample and record capability |

No marker means provenance was not recorded. Names never identify starter setups; legacy years are never declared reviewed.
Zero limited leave allocations remain valid stored values but carry a notice even after acknowledgement. No allocation is auto-filled.

## Storage and concurrency

`AcademicYear.ExtraProperties.HrSetupReviewV1` stores a versioned JSON string with the latest per-section actor ID, UTC time,
fingerprint and optional sample employee/date. No migration, reset or backfill is required. Unknown/corrupt evidence fails closed without overwrite.

Hashes include tenant/year/dates and ordered policy configurations with child rules/holidays. Attendance/payroll include calendar dependencies.
The sample includes all section hashes, selected staff concurrency stamp and actual resolution outcome/steps.
Invalidation is conservative: versions and audited DTO metadata are included, so renaming an unused year policy can require review again.

Reads/writes use serializable transactions. Writes compare the displayed hash and AcademicYear revision, then use ABP aggregate concurrency checking.
Two reviewers with the same revision cannot silently overwrite evidence. Configuration writes ordered after review can immediately make it stale on the next read;
review acknowledges captured settings and does not lock future configuration edits.
Clients refresh conflicts and request a new deliberate acknowledgement; they must not automatically resubmit a replacement hash.
`HR:SetupReview:Conflict` rejects stale input; `HR:SetupReview:Invalid` rejects unsupported sections, invalid state or unsupported stored evidence.
Database concurrency/serialization failures retain the existing error pipeline and require refresh/retry; failure is never reported as success.

## Sources and validation

- Contracts: `src/ShikshaMitra.Application.Contracts/HR/Policies/HrSetupReviewDtos.cs`.
- Application: `HrSetupReviewAppService`, configuration/validity/sample readers in `src/ShikshaMitra.Application/HR/Policies/`.
- Storage: `src/ShikshaMitra.Domain/TenantSettings/AcademicSetups/AcademicYears/AcademicYearHrSetupReview.cs`.
- Controller: `src/ShikshaMitra.HttpApi/Controllers/HR/Policies/HrSetupReviewController.cs`.
- Tests: `dotnet test test/ShikshaMitra.HttpApi.Tests --filter FullyQualifiedName~HrSetupReviewApi_Tests` with the repository-selected .NET 9 SDK.

Synthetic in-memory tests do not establish deployed PostgreSQL race behavior or real-school acceptance.
Roll out this additive API before mounting the [frontend checklist](../../../../../Shikshamitra-FE/docs/hr/references/setup-review.md).
