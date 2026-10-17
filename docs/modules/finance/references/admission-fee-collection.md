---
module: finance
domain: admission-fee-collection
content_type: Reference
status: canonical
updated_on: 2026-10-18
owner: backend-team
audience: humans-and-agents
---

# Collect fees during Admissions and Onboarding

Admissions and Onboarding open the existing Finance collection form in context. Collection remains optional; confirming a fee structure and recording a payment are separate actions. Ordinary Payment-stage completion requires recorded collection evidence; the explicit Collect later in Onboarding action requires a reason and allows progression without a payment or fee structure. Recorded evidence is not a settled/Paid status. Amounts, payment terms, collection/refund policies, receipt books and income mappings remain Finance-owned; no new school pricing configuration is introduced.

## API and authorization

The base route is `api/fee-collections/admission-applications/{applicationId}`.

| Action | Method/path | Exact Finance capability |
|---|---|---|
| Read balances | `GET summary` | `Finance.Fees.FeeCollections.View` |
| Prepare canonical dues | `POST prepare` | Collection Add and View |
| Read existing collection form | `GET collect-form` | Collection View |
| Record payment | `POST` at the base route | Collection Add |

Use the backend permission constants as the source of truth. Module-menu visibility grants are not required or automatically granted. Receipt links/downloads separately require Invoice Receipt View. The internal Student context reader verifies tenant, application/offering/cycle and current academic scope; it exposes only the facts Finance needs. It accepts exact Admissions/Onboarding lifecycle capabilities or exact Finance Collection read/write capabilities, without granting public Student detail access.

`prepare` accepts only Finance-configured variable-head initial amounts; fixed fees and all due amounts are server-derived. `CollectAdmissionFeeInput` accepts decimal allocations, counter instruments and an idempotency key, without accepting payer identity, Student ID or academic-year overrides. It maps into the existing collection workflow after validation. Existing Student collection JSON cannot set the internal `FinanceFeeAccountId` field.

## One ledger and lifecycle handoff

`FinanceFeeAccount` is the stable payer for an application/year before enrollment. Real `StudentId` is nullable; a reserved admission identity is never used as a Student. The existing assignment calculator creates the account's pinned calculation snapshot and canonical `DueFee` rows. Repeated preparation returns the same dues. Unused draft preparation may be replaced only after proving there are no payment/allocation/assignment effects.

The canonical collector writes dues, metadata, allocations, instruments, receipts, income entries and payment evidence in its serializable transaction. A repeated retry key with the same payment facts returns its original result; a changed payload is rejected. Student identity is excluded from admission retry fingerprints because it is linked later. Confirmed and pending-verification amounts remain separate; bank/cheque verification and reversals use existing Finance owners.

Finalization links the actual Student to existing financial rows. Activation creates or binds one real `StudentFeeStructure` using the pinned policies/calculation, and binds the existing dues to it. It preserves due, allocation, payment, receipt, income and refund identities, amounts, receipt bytes and stored retry results. Activation without a prepared account retains the existing non-payment flow. Existing compatible Student/year assignments are adopted without generating a second schedule; conflicting assignments are rejected.

After linking, Finance's ordinary Student collection entry resolves the same account. The Student form can read account dues before an enrollment assignment exists. Account-backed receipts, refunds, reconciliation, income, audit and monetary reports retain account-first scope and original academic facts rather than treating a null Student ID as an unscoped record.

Any payment evidence freezes the chosen fee structure and included heads, including pending or reversed transactions. Onboarding can confirm the identical saved snapshot without repricing it or requiring an archived pinned configuration to be republished. A choice saved before Admission finalization automatically satisfies Onboarding confirmation when there is payment evidence, no collection deferral, a matching assignment and current Finance-owned full settlement. Unsettled or deferred cases retain explicit Onboarding confirmation.

## Finance follow-up

The collection summary exposes `isSettled` only for a prepared, matching assignment whose Finance checkout ledger, approved charges and late fees are clear with no money awaiting verification. It also returns `canCollect=false` for that state. Admissions and Onboarding display the same settled balance and receipts read-only; a zero principal balance alone does not establish settlement. Unprepared or unknown balances remain distinct, and partial payments, outstanding charges, reconciliation, refund/reversal and retry contracts remain unchanged. The internal scoped `IAdmissionFinanceIntegration.IsSettledForOnboardingAsync` exposes only this Finance decision to Student readiness, without requiring a Finance mutation or preparing dues. Automatically satisfied Admission choices use a read-only Fees task and the same decision in pending queues, case progress and activation validation; no manual confirmation actor/time or payment is fabricated on a read. Existing manual confirmation history remains intact, and other activation requirements are unchanged.

Invoice Receipt detail/list contracts expose nullable `studentId` and optional `financeFeeAccountId`. Applicant receipt PDFs use the account's name snapshot. Settled fee receipts can open the existing refund request form with locked account, source receipt and actual academic-year context. Eligibility, reservations, caps, approval, payout verification and maker-checker rules remain in the existing Refund services. Cancellation does not automatically refund or waive a payment.

Pre-enrollment concession/adjustment and waiver workflows are not introduced. Student-only waiver operations reject a source without a real Student assignment; the contextual collection form does not display those controls. Standalone physical-form sales remain outside this Student collection entry.

Legacy `AdmissionPaymentTransaction` records contain a purpose code but no Finance fee-head or due allocation. Do not automatically credit those records against the new ledger: a form charge may be separate from assigned fees. Before rollout, inspect any existing legacy payments in the deployment database and have Finance reconcile their purposes explicitly. A local database with no legacy rows is not evidence that deployment data is also empty.

## Migration and acceptance

The current `AddAdmissionFeeAccounts` migration adds the account and nullable real-Student/account references, identity constraints and due uniqueness indexes. It preserves the three already-unmapped historical tables rather than dropping them. Its target snapshot matches the current mapped model. Downgrade is rejected while account rows exist, preserving financial evidence.

Required acceptance covers read-only summary, repeat preparation, repeated/changed payment keys, partial payments, pending verification and reversal, scope/tenant denial, unchanged paid confirmation, finalization/activation identity continuity, existing Student assignment adoption, Finance receipt/refund/report visibility and legacy Student collection. Source/unit tests, PostgreSQL concurrency, browser acceptance and applying the migration are separate evidence levels.

Collect later in Onboarding records a required reason in the Student-owned workflow history. It never calls preparation or collection, never creates an account, assignment, due or receipt, and never marks payment Paid/Waived. Saved collection evidence remains immutable when the outstanding balance is deferred.

Run the current account domain/model, admission collection lifecycle, selection-race and disposable PostgreSQL concurrency tests before rollout. Applying the migration and authenticated browser acceptance are separate checks; historical validation of an earlier checkout is not current deployment evidence.
