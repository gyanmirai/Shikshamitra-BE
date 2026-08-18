---
module: student
domain: architecture
content_type: Reference
status: canonical
updated_on: 2026-08-14
owner: backend-team
audience: humans-and-agents
---

# Locate Student Enrollment Admissions code

TL;DR: Admissions remains inside the Student modular-monolith boundary and separates aggregate invariants, authoritative validation, reads, commands, and runtime availability.

## Offering implementation map

| Concern | Source |
|---|---|
| Aggregate and effective dates | `src/ShikshaMitra.Domain/Students/StudentEnrollments/Admissions/AdmissionOffering.cs` |
| Deterministic availability | `src/ShikshaMitra.Domain/Students/StudentEnrollments/Admissions/AdmissionOfferingAvailability.cs` |
| DTOs and validators | `src/ShikshaMitra.Application.Contracts/Students/Enrollments/Admissions/AdmissionOfferingDtos.cs` and `AdmissionOfferingDtoValidators.cs` |
| Hierarchy and readiness validation | `src/ShikshaMitra.Application/Students/StudentEnrollments/Admissions/AdmissionOfferingConfigurationValidator.cs` |
| Scoped projections | `src/ShikshaMitra.Application/Students/StudentEnrollments/Admissions/AdmissionOfferingReadService.cs` |
| Commands and concurrency | `src/ShikshaMitra.Application/Students/StudentEnrollments/Admissions/AdmissionOfferingCommandService.cs` |
| Permission facade | `src/ShikshaMitra.Application/Students/StudentEnrollments/Admissions/AdmissionOfferingAppService.cs` |
| Inquiry and application guard | `src/ShikshaMitra.Application/Students/StudentEnrollments/Admissions/AdmissionOfferingRuntimeService.cs` |
| EF mapping and indexes | `src/ShikshaMitra.EntityFrameworkCore/Students/StudentEnrollments/Admissions/AdmissionOfferingConfiguration.cs` |
| HTTP routes | `src/ShikshaMitra.HttpApi/Controllers/Student/Enrollment/Admissions/AdmissionOfferingsController.cs` |

## Boundary rules

The domain aggregate owns normalized code, date inheritance, identity locking, and active state transitions.
The application validator owns repository-backed hierarchy, UserScope, published reference, cycle-window, and duplicate checks.
The read service applies tenant filters, academic UserScope, display projections, readiness, availability, semantic filters, and paging.
Inquiry and application services call the shared runtime service instead of duplicating availability logic.
The frontend owns interaction state only and cannot replace backend authorization or hierarchy validation.

## Persistence rules

Offering foreign keys use restrictive delete behavior.
Application and inquiry references to offerings use restrictive delete behavior.
The migration `src/ShikshaMitra.EntityFrameworkCore/Migrations/20260814180000_ProductionizeAdmissionOfferings.cs` makes override dates nullable, adds defaults, indexes runtime queries, and creates restrictive foreign keys.
