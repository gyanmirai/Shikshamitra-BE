---
module: student
domain: overview
content_type: Landing
status: canonical
updated_on: 2026-07-12
owner: backend-team
audience: humans-and-agents
---

# Understand the Student backend module

TL;DR: use this page as the entrypoint for Student enrollment, records, attendance, compliance, certificates, and dashboard behavior.
Student data is critical school data and must be protected for accuracy, tenant isolation, and auditability.

## Read order

| Step | File | Use it to |
|---|---|---|
| 1 | [product.md](./product.md) | Understand Student product areas and partial docs coverage |
| 2 | [prd-student-enrollment-master-blueprint.md](./prd-student-enrollment-master-blueprint.md) | Understand the complete future Student Enrollment journey and PRD ownership |
| 3 | [prd-inquiry-entrance-exam-crm.md](./prd-inquiry-entrance-exam-crm.md) | Understand inquiry-to-entrance-exam CRM behavior |
| 4 | [prd-admissions-playbook-registration-documents-exam-configuration.md](./prd-admissions-playbook-registration-documents-exam-configuration.md) | Understand playbooks, registration, documents, exam configuration, and readiness |
| 5 | [prd-final-admission-offers-seat-allocation.md](./prd-final-admission-offers-seat-allocation.md) | Understand offers, seats, waitlists, final decisions, withdrawal, and cancellation |
| 6 | [prd-enrollment-fees-concessions-payment-handoff.md](./prd-enrollment-fees-concessions-payment-handoff.md) | Understand admission quotations, concessions, payment gates, and Finance handoff |
| 7 | [prd-household-guardian-identity-compliance.md](./prd-household-guardian-identity-compliance.md) | Understand households, guardians, identity resolution, consent, and government readiness |
| 8 | [prd-student-activation-academic-placement-access.md](./prd-student-activation-academic-placement-access.md) | Understand Student conversion, numbering, placement, access, and activation repair |
| 9 | [prd-special-admission-routes.md](./prd-special-admission-routes.md) | Understand RTE, sibling, staff-child, transfer, re-entry, and other admission routes |
| 10 | [prd-indian-admission-policy-packs.md](./prd-indian-admission-policy-packs.md) | Understand sourced CBSE, Gujarat, RTE, and extensible state-board policy defaults |
| 11 | [prd-enrollment-operations-analytics-governance.md](./prd-enrollment-operations-analytics-governance.md) | Understand cross-stage operations, analytics, audit, retention, and data quality |
| 12 | [implementation-student-enrollment-master-blueprint.md](./implementation-student-enrollment-master-blueprint.md) | Review master-suite implementation status, constraints, migration proof, and release evidence |
| 13 | [implementation-admissions-playbook-registration-documents-exam-configuration.md](./implementation-admissions-playbook-registration-documents-exam-configuration.md) | Track companion PRD delivery phases and validation gates |
| 14 | [implementation-inquiry-entrance-exam-crm.md](./implementation-inquiry-entrance-exam-crm.md) | Track CRM implementation milestones, validation, and remaining gaps |
| 15 | [evaluation-inquiry-entrance-exam-crm.md](./evaluation-inquiry-entrance-exam-crm.md) | Evaluate current CRM evidence and choose the next implementation slice |
| 16 | [qa-student-enrollment-computer-use-bugs-2026-07-12.md](./qa-student-enrollment-computer-use-bugs-2026-07-12.md) | Review resolved Enrollment browser defects and verification evidence |
| 17 | [api.md](./api.md) | Find endpoint families and compatibility notes |
| 18 | [architecture.md](./architecture.md) | Locate Student source paths |
| 19 | [workflows.md](./workflows.md) | Trace inquiry, registration, admission, records, attendance, and certificate flows |
| 20 | [testing.md](./testing.md) | Pick focused Student validation commands |

## Source of truth

| Need | Source |
|---|---|
| Contracts | `src/ShikshaMitra.Application.Contracts/Students/**` |
| Application logic | `src/ShikshaMitra.Application/Students/**` |
| Domain rules | `src/ShikshaMitra.Domain/Students/**` |
| Persistence | `src/ShikshaMitra.EntityFrameworkCore/Students/**` |
| HTTP controllers | `src/ShikshaMitra.HttpApi/Controllers/Student/**` |
| Tests | `test/ShikshaMitra.*.Tests/Students/**` |
| Frontend integration docs | [Student frontend docs](../../../../Shikshamitra-FE/docs/student/README.md) |

## Ownership boundary

Backend Student docs own product rules, endpoint contracts, domain rules, persistence, and data integrity notes.
Frontend Student docs own routes, screens, React state, Redux Toolkit Query clients, and browser workflow coverage.

## For agents

Use this table before searching code:

| Question | First file |
|---|---|
| What does Student own? | [product.md](./product.md) |
| What is the complete future Student Enrollment journey? | [prd-student-enrollment-master-blueprint.md](./prd-student-enrollment-master-blueprint.md) |
| What is the future inquiry-to-entrance-exam CRM direction? | [prd-inquiry-entrance-exam-crm.md](./prd-inquiry-entrance-exam-crm.md) |
| What is the future registration, document, and exam configuration direction? | [prd-admissions-playbook-registration-documents-exam-configuration.md](./prd-admissions-playbook-registration-documents-exam-configuration.md) |
| How should offers, seats, waitlists, and final admission decisions work? | [prd-final-admission-offers-seat-allocation.md](./prd-final-admission-offers-seat-allocation.md) |
| How should enrollment fees, concessions, and payment conditions work? | [prd-enrollment-fees-concessions-payment-handoff.md](./prd-enrollment-fees-concessions-payment-handoff.md) |
| How should households, guardians, siblings, and identity readiness work? | [prd-household-guardian-identity-compliance.md](./prd-household-guardian-identity-compliance.md) |
| How should an approved applicant become an enrolled Student? | [prd-student-activation-academic-placement-access.md](./prd-student-activation-academic-placement-access.md) |
| How should RTE, transfer, sibling, staff-child, and returning routes work? | [prd-special-admission-routes.md](./prd-special-admission-routes.md) |
| Which CBSE, Gujarat, and RTE policy defaults apply? | [prd-indian-admission-policy-packs.md](./prd-indian-admission-policy-packs.md) |
| How should admissions operations, analytics, audit, and retention work? | [prd-enrollment-operations-analytics-governance.md](./prd-enrollment-operations-analytics-governance.md) |
| What is implemented across the complete Enrollment suite? | [implementation-student-enrollment-master-blueprint.md](./implementation-student-enrollment-master-blueprint.md) |
| How should the companion admissions PRD be implemented and validated? | [implementation-admissions-playbook-registration-documents-exam-configuration.md](./implementation-admissions-playbook-registration-documents-exam-configuration.md) |
| What CRM slices are implemented now? | [implementation-inquiry-entrance-exam-crm.md](./implementation-inquiry-entrance-exam-crm.md) |
| What CRM slice should be built next? | [evaluation-inquiry-entrance-exam-crm.md](./evaluation-inquiry-entrance-exam-crm.md) |
| Which Enrollment browser defects were found and resolved? | [qa-student-enrollment-computer-use-bugs-2026-07-12.md](./qa-student-enrollment-computer-use-bugs-2026-07-12.md) |
| Which endpoint should I use? | [api.md](./api.md) |
| Where is the code? | [architecture.md](./architecture.md) |
| Which workflow should I trace? | [workflows.md](./workflows.md) |
| Which tests should I run? | [testing.md](./testing.md) |
| Which frontend route or client owns a screen? | [Student frontend docs](../../../../Shikshamitra-FE/docs/student/README.md) |

## Validation

Run these commands from the `Shikshamitra-BE` repo root:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -Module student
git diff --check
```

Run the frontend Student docs check from `../../../../Shikshamitra-FE` when frontend docs or integration docs changed:

```bash
node scripts/docs/validate-docs.mjs --module student
```
