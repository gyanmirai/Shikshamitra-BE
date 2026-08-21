---
module: student
domain: architecture
content_type: Reference
status: canonical
updated_on: 2026-08-18
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

## Workflow implementation map

| Concern | Source |
|---|---|
| Definition, stage, and transition aggregates | `src/ShikshaMitra.Domain/Students/StudentEnrollments/Admissions/AdmissionWorkflowDefinition.cs`, `src/ShikshaMitra.Domain/Students/StudentEnrollments/Admissions/AdmissionWorkflowStageDefinition.cs`, and `src/ShikshaMitra.Domain/Students/StudentEnrollments/Admissions/AdmissionWorkflowTransitionDefinition.cs` |
| Graph validation | `src/ShikshaMitra.Domain/Students/StudentEnrollments/Admissions/AdmissionWorkflowGraphValidator.cs` |
| Controlled condition fields and evaluation | `src/ShikshaMitra.Domain.Shared/Students/StudentEnrollments/Admissions/AdmissionWorkflowConditionFieldRegistry.cs` and `src/ShikshaMitra.Domain/Students/StudentEnrollments/Admissions/AdmissionWorkflowConditionEvaluator.cs` |
| Deterministic transition selection | `src/ShikshaMitra.Domain/Students/StudentEnrollments/Admissions/AdmissionWorkflowTransitionSelector.cs` |
| Runtime handler registry | `src/ShikshaMitra.Domain/Students/StudentEnrollments/Admissions/AdmissionWorkflowStageHandlers.cs` |
| Runtime instances and history | `src/ShikshaMitra.Domain/Students/StudentEnrollments/Admissions/AdmissionApplicationRuntimeEntities.cs` |
| Configuration commands and queries | `src/ShikshaMitra.Application/Students/StudentEnrollments/Admissions/AdmissionWorkflowAppService.cs` |
| Runtime transition command | `src/ShikshaMitra.Application/Students/StudentEnrollments/Admissions/AdmissionApplicationAppService.cs` |
| HTTP routes | `src/ShikshaMitra.HttpApi/Controllers/Student/Enrollment/Admissions/AdmissionWorkflowsController.cs` and `src/ShikshaMitra.HttpApi/Controllers/Student/Enrollment/Admissions/AdmissionApplicationsController.cs` |

Form runtime persistence is separated from configuration: `AdmissionFormInstance` pins a published definition/version, `AdmissionFormFieldValue` stores mutable values, `AdmissionFormSubmission` stores immutable submitted snapshots, and `AdmissionFormChangeHistory` records lifecycle changes. Form definition changes never mutate an existing instance.

Draft graph updates synchronize existing child rows by identifier, insert new rows, and delete removed transitions before removed stages. This preserves required EF relationships and prevents conceptual-null failures while a draft is edited.

## Persistence rules

Offering foreign keys use restrictive delete behavior.
Application and inquiry references to offerings use restrictive delete behavior.
The migration `src/ShikshaMitra.EntityFrameworkCore/Migrations/20260814180000_ProductionizeAdmissionOfferings.cs` makes override dates nullable, adds defaults, indexes runtime queries, and creates restrictive foreign keys.
