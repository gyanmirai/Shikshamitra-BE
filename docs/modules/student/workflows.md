---
module: student
domain: workflows
content_type: Reference
status: canonical
updated_on: 2026-08-18
owner: backend-team
audience: humans-and-agents
---

# Trace Student Enrollment Admissions workflows

TL;DR: setup commands produce validated offering defaults, while inquiry and application runtime commands consume only offerings that are open and ready.

## Create or edit an offering

1. The controller authorizes the granular operation.
2. FluentValidation checks request shape and date presence.
3. `AdmissionOfferingConfigurationValidator` checks cycle, hierarchy, UserScope, dates, published references, and uniqueness.
4. Active offerings require complete readiness; inactive offerings may save missing runtime references for later completion.
5. The command service persists through the aggregate and returns a scoped detail projection.
6. Update compares the concurrency stamp and locks academic identity after an application exists.

## Activate, deactivate, and delete

Activation reruns full readiness validation before changing state.
Deactivation preserves all history and immediately blocks new inquiries and applications.
Delete compares concurrency and succeeds only when no application references the offering.

## Clone and bulk setup

Clone copies dates, workflow, inquiry form, application form, and default active state from the source.
The caller supplies a new cycle and academic identity, and the backend revalidates uniqueness and scope.
Bulk preview validates each standard and returns structured row errors.
Bulk commit inserts every row in one unit of work only when the preview-equivalent request is entirely valid.

## Inquiry and application runtime

Inquiry creation checks cycle consistency and shared offering availability before validating inquiry form values.
Application start checks the same availability service, validates any linked inquiry, and freezes the offering's published workflow and application form references.
Later changes to offering defaults affect only future runtime records.

## Configure and publish a workflow

1. Create a draft identity.
2. Add stages from the server stage-type registry.
3. Mark exactly one entry stage and at least one terminal stage.
4. Add controlled conditional transitions and at most one default route per source stage. Condition fields must use the server allowlist (`application`, `inquiry`, `assessment`, `documents`, `payment`, `eligibility`, `approval`, `offer`, `seat`, `enrollment`, or `appointment`).

Admission forms use the same controlled rule foundation for section visibility, field visibility, and conditional requiredness. The form runtime also supports canonical Applicant, Guardian, and Address bindings; these values remain domain-owned and are written back through the binding registry.
5. Validate reachability, dead ends, duplicate routes, cycles, terminal reachability, stage configuration, and published form references.
6. Publish only a valid draft.
7. Create a new immutable version for later edits.
8. Archive a published version only when no active offering uses it.

Visual display order never determines the entry stage or runtime routing.

## Advance an application workflow

1. Application start freezes a published workflow version and creates stage instances from the explicit entry stage.
2. A transition command supplies the expected current stage, application concurrency stamp, and an idempotency key.
3. The stage handler verifies that the current stage can complete.
4. Conditional routes are evaluated by priority, followed by the default route.
5. Completion or an authorized skip updates the current stage and next stage in one unit of work.
6. Reaching an explicit terminal stage completes the workflow instance.
7. Every transition creates immutable history and an Applicant 360 activity entry.
8. The response derives a deterministic next required action from the active stage type; completed workflows return no next action.
