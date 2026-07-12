---
module: student
domain: product-prd
content_type: Reference
status: canonical
updated_on: 2026-07-11
owner: backend-team
audience: humans-and-agents
---

# PRD: Indian admission policy packs

TL;DR: Provide sourced, versioned, reviewable CBSE, Gujarat, and extensible state-board admission defaults without hard-coding changing regulations or claiming legal compliance.

## Product intent

Indian school admission rules vary by board, state, class, school type, academic year, route, and government notification.
The product should convert sourced rules into reviewable defaults, validations, checklists, deadlines, and exception workflows.
A policy pack should help schools operate consistently while preserving the school's responsibility to verify and approve its applicable policy.

## Relationship to the enrollment suite

Admissions playbooks define school process.
Policy packs supply board, jurisdiction, and route defaults that playbooks reference.
When a playbook and policy pack differ, the product should identify the difference and apply the controlled precedence defined by the school.

## Goals

- Launch with CBSE and Gujarat policy coverage for private, aided, and trust-managed schools.
- Separate board rules, state rules, RTE rules, school policy, and operational guidance.
- Version every sourced rule with effective dates and supersession.
- Warn when a local override weakens or conflicts with a sourced default.
- Make policy evaluation and exceptions explainable to authorized staff.
- Extend to other Indian state boards without redesigning the enrollment workflow.

## Non-goals

- Do not provide legal advice or certify that a school is compliant.
- Do not encode an annual date, threshold, percentage, document list, or portal behavior without a source version.
- Do not treat public portal behavior as a stable API contract.
- Do not automatically submit data to a government or board system unless an approved adapter and authorization exist.

## Policy hierarchy

Policy sources should be classified as national law or scheme, state law or notification, board rule, government portal instruction, school policy, or internal operational guidance.
Applicability should consider tenant, school type, state, campus, board, medium, class, route, category, admission cycle, and effective date.
More specific applicable policy may override a general default only through configured precedence.
The product should show all applicable rules, the selected controlling rule, and unresolved conflicts.
No active applicant should change policy version silently after the journey starts.

## Policy-pack lifecycle

Policy-pack states should include Draft, Under Review, Approved, Published, Superseded, Withdrawn, and Retired.
Every published pack should identify source title, issuing authority, source URL or document reference, publication date when available, effective date, last verified date, reviewer, jurisdiction, applicability, and confidence note.
Publishing should require product or compliance review and school approval for tenant use.
Superseding a pack should identify changed rules and affected active cycles.
Authorized users should be able to simulate impact before applying a new pack to an active cycle.
Historical applicants should retain the pack version that governed each decision.

## Rule types

Policy packs should support age eligibility, class eligibility, qualifying examination, prior-school recognition, transfer certificate, migration certificate, marksheet, birth evidence, category evidence, income evidence, residence evidence, admission deadline, authority approval, seat pool, lottery or allotment, document validity, undertaking, admission-register, permanent-number, and government-reporting rules.
A rule should define condition, outcome, severity, blocking behavior, evidence, exception authority, parent-facing explanation, and staff guidance.
Severity should distinguish Information, Warning, Approval Required, and Blocking.
Rules should be deterministic where possible and should not rely on opaque scoring for legal or admission eligibility.

## Age eligibility

Age rules should be scoped by state, academic year, entry class, cutoff date, inclusive-boundary behavior, and documented relaxation.
The product should calculate age eligibility from the applicant's verified date of birth and display the exact cutoff rule.
Unknown or conflicting dates of birth should create a discrepancy rather than an automatic failure.
Relaxation should require the applicable source, authority, evidence, and approval.
CBSE packs should not define one national age cutoff because CBSE admission rules defer age limits to the applicable state or union territory.

## CBSE policy pack

The CBSE pack should model recognized prior-school or board requirements, qualifying examination, transfer evidence, date-of-birth evidence, late admission, class-specific eligibility, overseas equivalence, admission-register, and successive permanent-number expectations.
Class IX, X, XI, and XII rules should be distinct and versioned.
The pack should support approval or evidence requirements for cross-board, family-transfer, late, and overseas cases.
The product should not infer that a school-level entrance assessment replaces board eligibility.
Admissions through Class VIII should continue to evaluate the applicable state rules referenced by CBSE.

## Gujarat state and board policy pack

The Gujarat pack should distinguish Gujarat Education Department rules from Gujarat Secondary and Higher Secondary Education Board operational requirements.
The pack should support Gujarati and English-medium schools and should not assume one medium from the selected board.
Age eligibility should use the effective Gujarat notification for the relevant academic year and class.
GSEB or receiving-school evidence for previous results, migration, transfer, and school index references should be configurable from sourced requirements.
Where a public source does not establish an admission rule, the product should mark the item as school policy or pending official verification rather than infer it.

## Gujarat RTE policy pack

The Gujarat RTE pack should model annual application windows, district verification, seat allotment rounds, school re-selection when announced, allotment or admit-card reference, admission deadlines, documents, and cancellation consequences.
Annual dates, age cutoffs, document lists, and portal steps should be new pack versions for each cycle.
The pack should support authority-verified and school-verified evidence as separate states.
RTE seat allocation should be treated as an external authority decision that the school reconciles with its RTE seat pool.
The product should support later rounds and changed school choices without overwriting earlier allotment history.

## Government identity and data readiness

Policy packs may define readiness for PEN, APAAR, UDISE+, DigiLocker, board registration, or another approved government process.
Readiness rules should separate data completeness, consent, school review, external submission, external acceptance, mismatch, and retry.
APAAR-related policy should preserve the relationship to PEN and UDISE+ described by the official APAAR service.
The product should not treat locally valid data as proof of successful government registration.
Adapters and exports should use a separately approved interface contract and should remain replaceable.

## School adoption and overrides

Schools should review a policy pack before applying it to an admission cycle.
Adoption should record approver, effective cycle, selected rules, local additions, and unresolved warnings.
Overrides should require permission, reason, effective dates, impact preview, and authority.
The product should flag a weaker local rule and should require a second approval when configured.
An override should never modify the vendor-maintained source pack directly.

## Policy evaluation and exceptions

Every applicant evaluation should show Passed, Failed, Pending Evidence, Not Applicable, Exception Requested, Exception Approved, or Exception Rejected.
The evaluation should identify the exact rule and input values used.
Exceptions should identify authority, reason, evidence, validity, affected milestone, and any reporting obligation.
Expired evidence or exceptions should reopen the applicable readiness item.
Batch evaluation after a policy update should preview affected applicants before changing workflow states.

## Source register reviewed on 2026-07-11

| Source | Authority and product use |
|---|---|
| [CBSE Admission of Students to a School](https://www.cbse.gov.in/examin~1/admissionmt.htm) | CBSE source for age-rule delegation, transfer and qualifying evidence, class-specific admission, late admission, overseas eligibility, admission register, and successive admission numbers |
| [CBSE Affiliation Bye-Laws](https://www.cbse.gov.in/cbsenew/aff-bye-laws.html) | CBSE source family for school affiliation and record obligations that require separate applicability review |
| [Gujarat RTE admission portal](https://rte.orpgujarat.com/) | Gujarat Department of Primary Education operational source for annual RTE rounds, district verification, allotment, school admission, notices, and parent instructions |
| [Gujarat RTE Amendment Rules 2025 age notification](https://rte.orpgujarat.com/Content/Docs/ActRules/age_limit.pdf) | Gujarat Education Department source for the effective Standard 1 minimum-age rule and documented CwSN relaxation |
| [Gujarat RTE document guidance](https://rte.orpgujarat.com/CmsPage/Details/1) | Gujarat Department of Primary Education source for cycle-specific evidence guidance that must remain versioned |
| [GSEB school registration service](https://schoolreg.gseb.org/GeneralInstructions.aspx) | GSEB operational source for school registration interfaces and future adapter discovery, not by itself an applicant admission rule |
| [GSEB student services](https://studentserv.gseb.org/) | GSEB operational source for marksheet, certificate, migration, and result-verification services |
| [APAAR official service](https://apaar.education.gov.in/) | Ministry of Education source for APAAR, PEN, UDISE+, DigiLocker, prerequisite, and status-readiness research |
| [UDISE+ official service](https://udiseplus.gov.in/) | Ministry of Education source for UDISE+ workflow research and approved export or adapter discovery |

## Source maintenance

Every source should have an owner and scheduled review before each admission cycle.
Broken links, superseded notifications, contradictory sources, or inaccessible documents should create a policy-maintenance issue.
Annual Gujarat RTE notices should not be copied forward without current-cycle verification.
Unofficial articles, vendor blogs, and social media should not establish a policy rule.
Translations should link to the controlling source and identify the reviewer.

## Permissions and audit

Permissions should distinguish source maintenance, pack drafting, compliance review, publication, school adoption, local override, exception approval, and impact application.
Every publication, adoption, override, evaluation, exception, and supersession should be auditable.

## Analytics

Analytics should include pack adoption, applicants by policy version, failed and pending rules, exception requests, override use, evidence aging, policy conflicts, and time to resolution.
Reports should identify which policy version produced a decision.

## Acceptance scenarios

- A Gujarat CBSE school evaluates age using the applicable Gujarat rule rather than a national CBSE cutoff.
- A Class X or XII CBSE transfer case applies class-specific restrictions and evidence.
- A Gujarat RTE applicant retains the exact annual round, document, age, and deadline pack used for the decision.
- A school can add a stricter local document rule without changing the source pack.
- A weaker local override requires reason, impact preview, and configured approval.
- Publishing a superseding pack does not silently change historical applicants.
- An inaccessible or contradictory source blocks automatic policy publication and creates a review item.
- Users without permission cannot publish packs, apply overrides, or approve exceptions.

## Public concepts

Define Policy Pack, Policy Source, Source Authority, Jurisdiction, Applicability, Policy Version, Rule, Rule Severity, Controlling Rule, School Adoption, Local Override, Policy Conflict, Policy Evaluation, Policy Exception, Effective Date, Supersession, and Source Review.

## Delivery phases

Phase one should deliver the framework, reviewed CBSE and Gujarat defaults, Gujarat RTE annual pack, school adoption, controlled overrides, evaluation, exceptions, and source register.
Phase two should add more state boards, approved government exports or adapters, source-change monitoring, and richer impact simulation.
