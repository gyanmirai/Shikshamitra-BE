---
module: student
domain: product-prd
content_type: Reference
status: canonical
updated_on: 2026-07-11
owner: backend-team
audience: humans-and-agents
---

# PRD: Household, guardian, identity, and compliance

TL;DR: Resolve applicants into reusable people, households, guardians, and sibling relationships without duplicating identities or exposing protected family information.

## Relationship to the enrollment suite

Registration owns applicant-supplied family and identity responses.
This PRD owns household resolution, guardian authority, person matching, identity readiness, consent, and conversion into durable Student relationships.
Student Records owns long-term maintenance after activation.

## Goals

- Represent Indian family and care structures without forcing one father-mother household model.
- Reuse existing guardians and households safely across siblings.
- Prevent duplicate Student, guardian, and login identities.
- Preserve applicant-supplied, staff-supplied, verified, derived, and government-sourced provenance.
- Protect Aadhaar, custody, contact restrictions, financial responsibility, and sensitive documents.

## Non-goals

- Do not determine legal guardianship from unverified declarations.
- Do not make Aadhaar mandatory unless an applicable sourced policy requires it for the specific route and permits the school to collect it.
- Do not create government identifiers without consent and an approved external process.
- Do not replace the Identity and Access module's credential and authentication policy.

## Person and household model

A Person should represent one natural person independently from their role as applicant, Student, parent, guardian, grandparent, sibling, staff member, alumnus, or payer.
A Household should represent a reusable family or care unit with addresses, communication preferences, language, and linked people.
An applicant may belong to more than one household when care is shared.
One household may contain multiple current applicants, active students, former students, guardians, grandparents, or other approved relationships.
Shared household data should be reused without silently overwriting person-specific or verified values.

## Guardian relationships

Relationship types should include mother, father, legal guardian, foster guardian, grandparent, sibling, relative, local guardian, institution guardian, and school-defined relationships.
Each relationship should define effective dates, legal or care basis when supplied, primary contact status, emergency contact status, communication priority, fee responsibility, pickup authorization, portal access, and data visibility.
The product should support more than two guardians and should not infer authority from gender or relationship label alone.
Minor applicants should have at least one accountable contact before activation unless an authorized exception is recorded.
Changes to guardian authority should preserve prior values and evidence.

## Custody and contact restrictions

Authorized staff should be able to record protected custody, court-order, contact, communication, pickup, or disclosure restrictions.
Restrictions should be visible only to roles that require them for safety or administration.
Parent-facing surfaces should never reveal one guardian's protected address, contact, note, or restriction to another guardian without authorization.
Conflicting guardian instructions should block automated account linking or communication until reviewed.
Expired or withdrawn restrictions should remain in history with effective dates and authority.

## Address and communication responsibility

The product should support permanent, correspondence, local, emergency, and guardian-specific addresses.
Shared addresses should be linked rather than copied when the school confirms they represent the same location.
Communication preferences should be person-specific and channel-specific.
Household language preference should guide templates but should not override a guardian's explicit language choice.
Schools should be able to identify who receives academic, emergency, financial, admission, and general communication.

## Sibling, staff, and alumni relationships

Staff should be able to locate existing siblings using guarded matching on name, contact, guardian, address, school identifiers, and date of birth.
Sibling priority or concession should require a verified relationship to an eligible current or former student according to playbook.
Staff-child and alumni routes should reference the authoritative staff or alumni identity when available.
Unverified relationship claims should remain pending and should not automatically grant priority or concession.
Relationship evidence and decisions should be auditable.

## Identity capture and provenance

Identity attributes may include legal name, preferred name, date of birth, gender where required, nationality, birthplace, mother tongue, Aadhaar, PEN, APAAR ID, passport, previous-school identifiers, and board registration references.
Every value should identify whether it is applicant supplied, staff supplied, document extracted, document verified, government sourced, imported, or derived.
Verified values should not be silently overwritten by a later form submission or household merge.
Conflicts should create an identity discrepancy with both values, evidence, owner, and resolution history.

## Duplicate detection and resolution

Matching should consider applicant name, date of birth, guardian contacts, Aadhaar when lawfully available, PEN, APAAR ID, previous admission number, prior school identifiers, and sibling relationships.
The product should distinguish possible duplicate, likely duplicate, confirmed same person, confirmed different person, and unresolved match.
Authorized users should be able to link, merge, or continue as separate with a reason.
Merging should preview affected applicants, students, households, guardians, accounts, documents, payments, and communications.
Permanent identifiers, verified records, and audit history should never be discarded by a merge.

## Aadhaar, PEN, APAAR, and UDISE+ readiness

Aadhaar should be optional by default, masked in ordinary views, encrypted under platform policy, and restricted to authorized workflows.
Aadhaar consent, purpose, collection source, verification state, and revocation should be preserved when applicable.
PEN, APAAR, and UDISE+ readiness should use separate Not Required, Missing, Pending Consent, Ready, Submitted, Verified, Rejected, Mismatch, and Deferred states as applicable.
The product should validate required prerequisite data without claiming that local validation equals government acceptance.
Government or board submission adapters should remain pluggable and should preserve request, response, discrepancy, and retry history.
Manual exports and acknowledgements should be supported where a stable integration is unavailable.

## Account identity resolution

Before activation, the product should determine whether each guardian already has a tenant account.
Existing accounts should be linked only after guarded identity verification and relationship approval.
One guardian account may access multiple linked children without creating duplicate credentials.
Separate guardians should not be forced to share one account or email address.
Account creation should respect the chosen identifier, verified contact, language, role, scope, and recovery policy.

## Consent and privacy

Consent should be purpose-specific for admission communication, identity processing, optional government identifiers, document processing, and optional services.
Each consent should preserve text version, language, actor, channel, timestamp, and withdrawal state.
Withdrawal should stop future optional processing without erasing actions that must remain for audit or legal retention.
Retention rules should distinguish unsuccessful applicants, withdrawn applicants, activated students, identity evidence, custody evidence, and government acknowledgements.
Exports and downloads of sensitive identity data should require permission and audit.

## Operations workspace

Queues should include likely duplicate people, unresolved household match, guardian conflict, missing accountable contact, custody review, identity discrepancy, sibling verification, Aadhaar review, PEN or APAAR readiness, account-link conflict, and consent exception.
Resolution screens should show source values and evidence side by side without exposing unrelated household data.
Bulk identity merges or custody decisions should not be allowed.

## Permissions and audit

Permissions should distinguish household view, protected relationship view, household edit, guardian authority, identity verification, duplicate merge, Aadhaar access, government readiness, account linking, and export.
Every merge, link, split, guardian-authority change, restriction, identity verification, consent, and government-state change should be auditable.

## Analytics

Analytics should include duplicate rate, merge rate, unresolved identity aging, household reuse, sibling verification, guardian contact readiness, consent completion, government-readiness status, and account-link failures.
Leadership analytics should use aggregated results and should not expose identity numbers or protected family information.

## Acceptance scenarios

- Two siblings reuse the same verified guardians and household without duplicating people.
- An applicant can have multiple guardians with different communication, pickup, fee, and portal responsibilities.
- A protected custody restriction is visible to authorized staff and hidden from unauthorized guardians.
- A verified identity value is not overwritten by a conflicting registration response.
- A likely returning student is reviewed before a second Student identity is created.
- Aadhaar remains masked and optional unless the applicable governed route requires and permits collection.
- A guardian with an existing account gains access to the new child without receiving duplicate credentials.
- An identity or account-link conflict blocks first-day readiness and enters a resolution queue.
- Users without permission cannot view Aadhaar, protected restrictions, merge identities, or export sensitive data.

## Public concepts

Define Person, Household, Household Membership, Guardian Relationship, Accountable Contact, Communication Responsibility, Fee Responsibility, Pickup Authorization, Custody Restriction, Identity Provenance, Identity Discrepancy, Duplicate Person Match, Aadhaar Consent, PEN Readiness, APAAR Readiness, UDISE+ Readiness, and Account Link.

## Delivery phases

Phase one should include household resolution, guardian roles, sibling reuse, duplicate protection, identity provenance, protected restrictions, account matching, and consent.
Phase two should add government export readiness, richer discrepancy resolution, household self-service, and approved external adapters.
