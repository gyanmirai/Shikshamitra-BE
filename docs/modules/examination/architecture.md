---
module: examination
domain: architecture
content_type: Reference
status: canonical
updated_on: 2026-08-31
owner: backend-team
audience: humans-and-agents
---

# Locate Examination seating implementation

TL;DR: contracts and validation live in Application.Contracts, subject ownership and workflow behavior in Application, plan lifecycle in Domain, and persistence mappings in EntityFrameworkCore.

| Layer | Source | Ownership |
|---|---|---|
| HTTP | `src/ShikshaMitra.HttpApi/Controllers/Academic/Examination/Exams/SeatAllocationController.cs` | Routes and permission attributes |
| Contracts | `src/ShikshaMitra.Application.Contracts/Academic/Examination/SeatAllocation/**` | Workspace, preview, validation, version, and FluentValidation shapes |
| Application | `src/ShikshaMitra.Application/Academic/Examination/SeatAllocation/ExamSeatAllocationAppService.cs` | Scope checks, grouping, readiness, allocation, lifecycle, audit |
| Domain | `src/ShikshaMitra.Domain/Academic/Examination/SeatAllocation/ExamSeatingPlan.cs` and `ExamSeatingPublication.cs` | Plan scope plus the single examination publication boundary |
| Persistence | `src/ShikshaMitra.EntityFrameworkCore/Academic/Examination/SeatAllocation/**` | Unique subject ownership, plan session compatibility, and one publication per exam |
| Migration | `src/ShikshaMitra.EntityFrameworkCore/Migrations/20260831105310_AddExaminationSeatingPublicationLifecycle.cs` | Safe duplicate cleanup, ownership constraint, publication table, and qualified legacy backfill |

`ExamSeatingPlanSubject` is the plan-to-exam-subject ownership table.
The V2 plan endpoint creates a null-session-key plan and chooses `SingleSubject` or `SelectedSubjects` from the selected ID count.
The compatibility session endpoint can still assign the immutable session key when all selected subjects share one complete time slot.
Do not bypass `EnsureSubjectWiseSubjectsAreAvailableAsync`; it enforces one-plan ownership across all scope types. `ExamSeatingPublication` existence is authoritative: plan-level `Published` flags alone do not establish global publication. Clean, complete legacy publication is backfilled; partial or historically duplicated legacy publication is reopened as a draft. The migration stops for manual repair rather than guessing when two populated legacy plans own the same subject.
