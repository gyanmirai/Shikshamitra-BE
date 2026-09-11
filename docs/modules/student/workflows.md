---
module: student
domain: workflows
content_type: Reference
status: canonical
updated_on: 2026-09-11
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

## Run a managed Inquiry

1. Enable the Inquiry policy in a draft workflow and choose the application-start requirement.
2. Configure allowed sources, required source detail, duplicate behavior, identity requirements, counsellor assignment, deadlines, follow-up defaults, and qualification checklist as needed.
3. Validate and publish the workflow, then attach it to an offering whose forms and counsellor scope are compatible.
4. Create an inquiry; the server snapshots policy and deadlines, assigns a counsellor when configured, and records duplicate-override context in audit activity.
5. Record meaningful contacts and schedule, complete, or cancel follow-ups from the management workspace.
6. Record the checklist-backed qualification outcome and reason.
7. Start an application only after the linked Inquiry satisfies its pinned start requirement.
8. Read historical management after application start; mutable Inquiry management closes when the inquiry reaches application-started or converted state.

Basic workflows retain the existing inquiry commands and do not create managed records.

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

## Run a managed entrance assessment

1. Add an Assessment stage to a draft workflow, enable its assessment policy, and select result-only or scheduled operation.
2. Configure attendance, scoring, review, and attempts; add explicit routes for negative outcomes when those outcomes should leave the stage.
3. Validate and publish a new version, then attach it to an offering for future applications.
4. For scheduled operation, create shared future sessions for the offering and exact workflow stage.
5. While an applicant is in the assessment stage, create an attempt and reserve a matching session when required.
6. Record attendance and results after the session starts, or directly in result-only mode according to its attendance requirement.
7. If review is required, a different authorized reviewer approves the result or returns it for correction.
8. For failed or absent results, request an allowed retake and create another attempt, record an authorized waiver, or use an explicit configured outcome route.
9. Complete the stage through the existing workflow advance command; completion remains separate from recording evidence.

The assessment summary returns capability flags for each permitted action and explains missing evidence, pending review, future sessions, and missing outcome routes.
Changes after an assessment stage has completed are rejected; its attempts remain readable within tenant and academic scope.

## Verify applicant documents

1. Configure a managed Document Verification stage in a new workflow version, including applicability, collection mode, formats, limits, catalog mapping, independent review, and optional waivers.
2. Publish validated settings and use the new version for future applications. Existing applicants keep their pinned requirements.
3. At the active document stage, collect uploads or record an original-document observation. Each action creates a new evidence revision with its collector identities.
4. A permitted reviewer accepts complete evidence or requests replacement with a reason. Rejection is available before an original check; acceptance still requires every configured collection component.
5. Replace rejected evidence, record a fresh original check when required, and review the new revision. Earlier revisions and decisions remain available as history.
6. Resolve all mandatory and matched conditional requirements, or record a separately authorized policy-supported waiver with a reason. Missing condition inputs block continuation.
7. Complete the workflow stage explicitly. Normal completion, manual skip, and administrator override share the mandatory-evidence gate.
8. Finalize admission through the existing accepted-offer and completed-workflow process. Link canonical evidence to the Student in the finalization unit of work and continue operational onboarding.

Mapped accepted uploads may satisfy corresponding onboarding document uploads. Where an admission waiver, physical-only check, or custom document leaves a mandatory onboarding upload outstanding, authorized staff use the scoped active-onboarding document upload flow. This preserves the separate onboarding readiness rule while providing a route to complete it.

## Check eligibility

1. Enable managed Eligibility in a draft workflow and add only the school's required criteria. Select published source forms or earlier managed source stages explicitly.
   Select those same application and inquiry form versions when configuring the Offering; incompatible selections cannot be activated or used for new applicants.
2. Validate and publish a new version. Existing applications retain their pinned policy and source stage identities.
3. At Eligibility, check the saved information. Review each criterion's expected value, actual value, source and explanation.
4. Correct unknown or invalid information at its source. If the application form must be reopened, resubmit it, then check eligibility again; resubmission does not automatically complete managed Eligibility.
5. For a genuine failing criterion, request an allowed exception with a reason. The configured authorized reviewer decides it independently when required; the original result remains in history.
6. Continue only with fresh evidence. Explicit negative-result routes may send an ineligible or incomplete application for review, while admission rejection remains a separate decision.
7. Recheck a visited Eligibility stage if source information changes later. First-time finalization verifies fresh passing or exception-approved evidence; successful finalization retries remain idempotent.

## Managed Review workflow

1. Publish a version 1 Review policy with any earlier managed prerequisites, reviewer/approver restrictions and checklist.
2. Enter Review; the server initializes the assignment/deadline work item without making an admission decision.
3. Read the current evidence and blockers, optionally assign an authorized reviewer, and save a draft or submit a reasoned recommendation.
4. An authorized approver uses the existing admission Approve, Waitlist or Reject command, or explicitly requests an application-form correction.
5. A correction reopens the form; save and resubmit remain at Review and require a new recommendation.
6. Complete Review normally after the actual decision; negative outcomes require an explicit matching route.
7. Before offering or finalization, check that the approved recommendation still matches current evidence; refresh the completed Review and reaffirm approval when a correction made it stale.

Changing an assignment or saving a newer draft supersedes an earlier recommendation until resubmitted.
Review history and actual decisions remain immutable, with the actor, time and safe prerequisite/checklist snapshots retained.
An overdue queue is available for scoped active Review stages; this delivery does not send deadline reminders.

## Run a managed Form stage

1. Enable Form management in a draft workflow, select the same published application form used by the Offering, and configure only the optional behavior the school needs.
2. Publish the workflow and activate a compatible Offering. New applications freeze both versions; existing applications keep their original configuration.
3. Save drafts when permitted, then submit through the existing dynamic form. Required-field, deadline, and attempt checks run on the server.
4. If verification is configured, authorized staff verify each selected section. Revoking verification requires a reason.
5. Request a correction with a reason when allowed. The form returns to draft and every previous verification becomes stale.
6. Resubmit and repeat verification. Continue only when the server reports no blockers; automatic advancement is used only when the policy permits it.

All actions retain immutable form submissions and change history. Managed Form settings require no new persistence table or database migration.

## Run a managed Payment stage

1. Add Payment to a draft workflow and choose **Managed collection with financial controls**.
2. Set the purpose, currency, amount, payer, staff methods, deadline, installment rules, second-person verification, and waiver policy.
3. Validate and publish the workflow. Online gateway choices block publishing until the tenant has a trusted gateway.
4. In the application, record received money. Non-cash methods require the bank or provider reference; the server generates the admission receipt number.
5. If independent verification is enabled, a different authorized staff member verifies or rejects the payment.
6. Continue after the full amount settles, or record an authorized full waiver with a reason. A reversal reopens the outstanding balance while the stage is active.

Transition rules may use only `payment.status`, `payment.settledAmount`, and `payment.outstandingAmount`.

## Run a managed Offer stage

1. Add Offer to a draft workflow and enable the managed offer process.
2. Choose unconditional or conditional content, terms and confirmations, validity and override limits, decline reasons, extension/reissue limits, allowed completion outcomes, and acceptance continuation.
3. Add explicit `offer.status` transitions for every enabled declined, expired, or withdrawn outcome. Acceptance may use the normal default path.
4. Validate and publish the workflow. Existing applications and basic Offer stages keep their pinned behavior.
5. At the active stage, issue the offer. Staff may extend or withdraw it only when the server capability permits the action.
6. Record acceptance with the required confirmations, or record a decline reason. Expired, declined, and withdrawn offers may be reissued within the configured limit.
7. Continue only after the server reports an allowed completed outcome and safe route.

The application card shows the current version, type, expiry, conditions, blockers, available actions, and permanent event history. This workflow records staff-mediated responses; communication delivery and applicant self-service are separate integrations.

## Run a managed Seat stage

1. Add Seat to a draft workflow and enable managed capacity and reservation.
2. Set the offering capacity, reservation timing and duration, renewal and waitlist rules, authorized exceptions, release reason, completion outcomes, and confirmation continuation.
3. Add an explicit `seat.status` transition for every enabled waitlisted, released, or expired completion outcome. Confirmation may use the default route.
4. Validate and publish the workflow. Existing applications and basic Seat stages keep their pinned behavior.
5. Reserve manually, or let stage entry reserve automatically. A full offering can place the applicant on the FIFO waitlist when enabled.
6. Confirm, renew, release, promote, or adjust capacity only when the returned server capability permits it. Every action records immutable history.
7. Continue after a permitted outcome. A confirmed allocation remains visible and releasable after advancement until admission finalization.

Capacity, expiry, and waitlist facts are computed by the server. Staff should refresh and retry after a concurrency conflict instead of overwriting a newer allocation decision.
