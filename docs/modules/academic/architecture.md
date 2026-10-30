---
module: academic
domain: architecture
content_type: Reference
status: canonical
updated_on: 2026-10-29
owner: backend-team
audience: humans-and-agents
---

# Locate Academic guide implementation

TL;DR: the guide adds bounded metadata to existing audited Tenant and AcademicYear aggregates and reads existing configuration records; it adds no entity table, migration, permission catalogue or seeder.

## Source ownership

Academic Insights is an additive read model under `Application/Academic/Dashboard/AcademicInsights*.cs`.
`AcademicInsightsContextReader` validates the tenant-owned hierarchy and resolves allowed divisions before feature readers run.
Separate teaching, task, student, operational, and personal readers own bounded projections; `AcademicInsightsAppService` checks exact section permissions and composes the response.
`AcademicInsightsSectionAccess` prevents denied sources from being invoked.
`HttpApi/Controllers/Academic/Dashboard/AcademicInsightsController.cs` delegates the additive GET route, and `Application.Contracts/Academic/Dashboard/AcademicInsightsDtos.cs` owns its payload.
The personal reader reuses persisted portal association and mobile placement/services. The read model adds no persistence, migrations, permission definitions, or seeders.

| Layer | Source | Responsibility |
|---|---|---|
| HTTP | `src/ShikshaMitra.HttpApi/Controllers/Academic/Setup/AcademicSetupController.cs` | Four explicit routes and DTO delegation |
| Contracts | `src/ShikshaMitra.Application.Contracts/Academic/Setup/**` | Visit, context, progress and state shapes |
| Application | `src/ShikshaMitra.Application/Academic/Setup/AcademicSetupAppService.cs` | Year progress orchestration and response filtering |
| Authorization | `src/ShikshaMitra.Application/Academic/Setup/AcademicSetupAccess.cs` | Active identity, exact actions and hierarchy context |
| First visit | `src/ShikshaMitra.Application/Academic/Setup/AcademicSetupVisitService.cs` | Read-only gating and fresh-UOW compare-and-swap retry |
| Configuration query | `src/ShikshaMitra.Application/Academic/Setup/AcademicSetupConfigurationReader.cs` | Sequential authorized existence projections |
| Domain metadata | `src/ShikshaMitra.Domain/Academic/Setup/Services/**` | Versioned storage and invariants |
| Shared definitions | `src/ShikshaMitra.Domain.Shared/Academic/Setup/AcademicSetupKeys.cs` | Exact stages, keys and business error codes |

## Persistence and concurrency

| Aggregate and repository | Extra property | Stored data |
|---|---|---|
| Tenant through `ITenantRepository` | `AcademicConfigurationFirstVisitV1` | Version 1, first actor ID and visit time; at most 1024 characters |
| AcademicYear through `IRepository<AcademicYear, Guid>` | `AcademicSetupGuideProgressV1` | Version 1, cursor, deferred keys, review actor/time; at most 4096 characters |

The visit helper preserves an existing valid marker unchanged.
The visit service uses the tenant aggregate's optimistic concurrency and starts a fresh transactional unit of work for each of at most three attempts.
A losing write reloads the winning marker and returns `firstVisit: false`.
Exhausted concurrency conflicts remain retryable HTTP 409 responses.

Year writes compare the submitted stamp with the current AcademicYear stamp and then save transactionally.
The existing aggregate stamp also protects unrelated year changes and HR review metadata, so a conflict may require refetching even when only another year property changed.
The code changes only its own ExtraProperties key and preserves sibling metadata.
Unknown versions, missing required stored progress fields and malformed JSON raise a business error instead of being replaced with defaults.

The pattern follows existing `src/ShikshaMitra.Domain/TenantSettings/AcademicSetups/AcademicYears/AcademicYearHrSetupReview.cs`.
No migration or database reset is needed because both owner aggregates already persist ExtraProperties and concurrency stamps.

## Configuration projections

Year ownership follows medium to education board to academic year.
Class ownership follows standard to medium stage to medium.
Unauthorized settings are skipped before any configuration query.
EF operations run sequentially in the request's unit of work.

| Keys | Existing records and qualification |
|---|---|
| Subjects, elective groups | Selected medium and active `SubjectMasterStatus` |
| Weekly lectures, lecture slots | Selected class's `WeeklyLectureSlot` or `LectureSlotConfig` |
| Homework types, assignment types | Selected medium and active `TaskTypeStatus` |
| Rubrics | Selected medium, reusable template, nonarchived/nonsuperseded, institution visibility or caller ownership; null class or selected class |
| Grading scales, exam types | Selected medium and active or system-default `SettingStatus` |

These projections report record existence only.
They do not load usage graphs or replace domain-specific readiness checks.
Existing configuration services in `src/ShikshaMitra.Application/Academic/**` continue to own creates, updates, validation, archive rules and their action permissions.
Frontend wiring remains documented in the [frontend clients guide](../../../../Shikshamitra-FE/docs/academic/clients.md).
