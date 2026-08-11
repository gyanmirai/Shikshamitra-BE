---
module: student
domain: api
content_type: Reference
status: canonical
updated_on: 2026-08-10
owner: backend-team
audience: humans-and-agents
---

# Use Student backend APIs

TL;DR: use this page to find Student endpoint families and the refined Enrollment contract boundaries.
Controller source paths are canonical when route details change.

## Enrollment API families

| Family | Controller path | Requirement role |
|---|---|---|
| Admissions CRM | `src/ShikshaMitra.HttpApi/Controllers/Student/Enrollment/AdmissionsCrm/**` | Pipeline, assessments, visits and events, registration, Finance handoff display, placement review, activation, and communication |
| Enrollment settings | Admissions CRM catalog and configuration controllers | Compatibility settings such as Inquiry Source where still active |
| Direct student registration and notes | `src/ShikshaMitra.HttpApi/Controllers/Student/Enrollment/**` | Existing active-student registration details and notes outside the Admissions CRM aggregate |

## Records API families

| Family | Controller path |
|---|---|
| Student list | `src/ShikshaMitra.HttpApi/Controllers/Student/Records/StudentListController.cs` |
| Student management | `src/ShikshaMitra.HttpApi/Controllers/Student/Records/StudentManagementController.cs` |
| Bulk upload | `src/ShikshaMitra.HttpApi/Controllers/Student/Records/StudentBulkUploadController.cs` |
| Dashboard | `src/ShikshaMitra.HttpApi/Controllers/Student/Records/StudentDashboardController.cs` |
| Certificates | `src/ShikshaMitra.HttpApi/Controllers/Student/Records/StudentCertificateController.cs` |
| House settings | `src/ShikshaMitra.HttpApi/Controllers/Student/Records/Settings/HouseSettingsController.cs` |

## Attendance, leave, and compliance API families

| Family | Controller path |
|---|---|
| Student attendance | `src/ShikshaMitra.HttpApi/Controllers/Student/Attendance/StudentAttendanceController.cs` |
| Student leave | `src/ShikshaMitra.HttpApi/Controllers/Student/Attendance/StudentLeaveController.cs` |
| Student leave type settings | `src/ShikshaMitra.HttpApi/Controllers/Student/Attendance/Settings/StudentLeaveTypeController.cs` |
| Identity compliance | Student identity compliance controller source paths |

## Inquiry Source compatibility

Inquiry Source is exposed through the existing `inquiry-preferences` route for compatibility.
Product copy should say Inquiry Source, while route and DTO names can still use Inquiry Preference.

| Route | Purpose |
|---|---|
| `/api/student-enrollment/{academicYearId}/settings/inquiry-preferences` | List or create Inquiry Sources |
| `/api/student-enrollment/settings/inquiry-preferences/{id}` | Get, update, or delete one source |
| `/api/student-enrollment/settings/inquiry-preferences/{id}/status` | Activate or deactivate one source |

## Refined Admissions CRM contract

The CRM route family is `api/student-enrollment/admissions-crm`.
Every authenticated Enrollment workflow must validate tenant and academic scope on the server.
Frontend route visibility is not authorization.
Every applicant must have a Pipeline lead before assessment, registration, Finance handoff, placement review, or activation.

| Requirement area | Contract expectation |
|---|---|
| Admission Year Setup | Read and update admission-specific rules attached to an Institute Setup academic year |
| Applicant Stepper | Return applicant step status, blockers, skipped steps, and next action |
| Pipeline | Create and list leads by academic year, source, inquiry type, owner, desired board, medium, standard, and stage; return one immutable database-generated `ApplicantNumber` for every lead |
| Owner assignment | Assign or clear lead owner with permission and timeline evidence |
| Follow-up | Create, list, and complete lead tasks |
| Messaging | Render, send, schedule, and cancel approved simple reminders with consent, duplicate-message protection, and delivery-log state |
| Assessment | Schedule an entrance exam, interaction, or interview; record attendance and results; decide eligibility; preserve evidence |
| Registration | Issue, save, submit, correct, verify, and review document checklist |
| Finance handoff | Request or display Finance-owned verification state without posting money |
| Placement | Read placement options from academic setup and capacity sources, then record selected timing or division |
| Activation | Preview, request, approve, execute, and repair activation after required gates pass |
| Admission Reports | Return lifecycle, source, standard, and closed-outcome counts after tenant, academic-year, UserScope, date, and standard filters |
| Admission intelligence | Return capacity, projected surplus or shortfall, first-response risk, abandonment, completion, overdue, and SLA indicators without bypassing scope |
| Scheduled reports | Validate active internal recipients and send authenticated report links without email attachments containing applicant data |
| Communication settings | Configure templates, channels, variables, provider mode, write-only provider credentials, and delivery-log behavior |

## Key route patterns

| Route pattern | Purpose |
|---|---|
| `/{academicYearId}/leads` | Create and list Pipeline inquiries |
| `/{academicYearId}/quick-inquiry-options` | Return safe capture options such as enabled channels, sources, routes, and standards |
| `/{academicYearId}/operations/my-work` | Return assigned and due follow-up work for the current user |
| `/workspace/applicants/{leadId}/progress` | Return the canonical six-step applicant projection |
| `/leads/{leadId}/owner` | Assign or clear owner |
| `/leads/{leadId}/tasks` | Create or list follow-up tasks |
| `/tasks/{taskId}/complete` | Complete a task |
| `/leads/{leadId}/messages/send-template` | Send an approved message template and create delivery evidence |
| `/{academicYearId}/exams` | List assessment records; the stable API segment remains `exams` |
| `/leads/{leadId}/exam/schedule` | Schedule or reschedule an assessment |
| `/exams/{examRecordId}/attendance` | Mark exam attendance |
| `/exams/{examRecordId}/result` | Record exam result |
| `/exams/{examRecordId}/eligibility` | Decide eligibility, ineligibility, retest, or review |
| `/leads/{leadId}/registrations/readiness` | Return the resolved registration path and structured issuance blockers |
| `/leads/{leadId}/registrations/issue` | Issue one shared registration draft in Online, Staff-assisted, or Both mode; invitation failure is returned separately |
| `/registrations/{applicationId}/invitation/send` | Send or retry the secure online-registration invitation without issuing a duplicate registration |
| `/registrations/{applicationId}/staff-responses` | Save staff-entered registration responses |
| `/registrations/{applicationId}/staff-submit` | Submit staff-entered registration responses |
| `/registrations/{applicationId}/verify` | Verify registration |
| `/registrations/{applicationId}/document-checklist` | Review document readiness |
| `/leads/{leadId}/documents/send-email` | Generate and issue an enabled applicant document, then email a re-rendered PDF attachment with recoverable delivery metadata |
| `/leads/{leadId}/activation/preview` | Preview activation gates and placement readiness |
| `/leads/{leadId}/activation` | Request activation |
| `/activation/{activationId}/approve` | Approve activation |
| `/activation/{activationId}/execute` | Execute activation |
| `/{academicYearId}/activation/placement-options` | Return placement options from valid academic hierarchy and capacity sources |
| `/{academicYearId}/reports/admissions` | Return the focused Admission Reports projection |
| `/{academicYearId}/analytics/advisory-forecast` | Return scoped advisory forecast, capacity, response-risk, and SLA indicators |
| `/{academicYearId}/governance/report-schedules` | Create or list scheduled report definitions; internal recipients use `user:{userId}` |
| `/{academicYearId}/governance/report-schedules/run-due` | Generate due expiring exports and deliver authenticated links for internal recipients |
| `/{academicYearId}/operations/overview` | Return operational counts for My Work and Pipeline |
| `/setup/admission-years/{academicYearId}/versions` | List versioned Admission Year Setup records |
| `/setup/admission-years/{academicYearId}/standard-options` | List Institute Setup standards that can be admitted |
| `/setup/admission-years/{academicYearId}/draft` | Create the next Admission Year Setup draft |
| `/setup/admission-years/drafts/{id}` | Update a draft with concurrency protection |
| `/setup/admission-years/{id}/publish` | Validate and publish an immutable setup version |
| `/setup/admission-years/{id}/close` | Close a published setup with an audit reason |
| `/setup/cycles` | Compatibility route for the older academic-year setup list |
| `/setup/cycles/{academicYearId}/summary` | Compatibility route for the older admission-year summary |
| `/{academicYearId}/configuration/cycle` | Existing code-level route for admission-year configuration |
| `/{academicYearId}/configuration/pipeline-stages` | Existing code-level route for fixed Pipeline stage labels and metadata |
| `/{academicYearId}/configuration/registration-templates` | Configure registration templates |
| `/{academicYearId}/configuration/message-templates` | Configure message templates |
| `/{academicYearId}/communication/providers` | Read provider configuration with credential values redacted |
| `/{academicYearId}/communication/providers/{channel}` | Update one channel and its write-only provider credentials |

The existing route name `setup/cycles` may remain in API code for compatibility.
User-facing copy should use Admission Year Setup unless a specific code route is being discussed.
`ApplicantNumber` is the short staff-facing Enrollment identifier. It is immutable after lead creation,
searchable with or without the `#` display prefix, and projected into Pipeline, Assessments,
Operations Queue, My Work, and applicant-detail responses. The internal GUID remains the API route key.

## Admission Reports contract

Admission Reports requires `Students.Enrollment.AdmissionsCrm.Analytics`.
The report is always limited to the current tenant, requested academic year, and effective Student UserScope.
`FromDate` and `ToDate` filter applicant creation dates and include the complete selected calendar dates.
The calendar-date span cannot exceed 366 days.
`TargetStandardId` narrows the report after server-side scope enforcement.
The response contains summary totals and fixed lifecycle, source, standard, and closed-outcome groupings.
Lifecycle classification is shared with Pipeline so report counts do not drift from the operational board.

## Provider credential contract

Provider header values, provider parameter values, and callback secrets are write-only.
Read responses retain configured header and parameter names but replace their values with empty strings.
Read responses expose `HasStoredHeaderValues`, `HasStoredParameterValues`, and `HasCallbackSecret` so the UI can show that credentials exist.
A blank value for an existing header or parameter preserves the stored value during an update.
Omitting a header or parameter key removes that key from the saved settings.
A blank callback secret preserves the stored secret, and a nonblank value replaces it.
The frontend must remove read-only stored-value flags before sending an update.

## Finance boundary

Enrollment APIs may request or display Finance-owned verification status.
Enrollment APIs must not create receipts, post ledgers, settle payments, reconcile transactions, or issue refunds.
Activation must block when fee verification is required and Finance has not returned a verified state.

## Placement boundary

Placement APIs must use Institute Setup and Class Management as the source of truth for board, medium, standard, division, capacity, active count, and remaining count.
If capacity cannot be verified, the API should return an unknown capacity state rather than a fabricated seat count.
Capacity override requires permission and reason.

## Permission and scope rules

| Action family | Required control |
|---|---|
| Pipeline read | CRM view permission and academic scope |
| Pipeline capture | CRM add permission and academic-year validity |
| Owner and follow-up | CRM operation permission and lead scope |
| Exam result and eligibility | Exam operation permission, correction reason for replacement, and academic scope |
| Registration verification | Registration verification permission and document access control |
| Finance status visibility | Finance handoff permission or role-appropriate Enrollment permission |
| Placement override | Placement or capacity override permission and reason |
| Activation | Activation preview, request, approve, execute, or repair permission according to the action |
| Communication templates | Configuration permission |
| Sending messages | Communication permission plus consent and opt-out checks |

## Public admissions API

Public inquiry and status routes provide parent-facing capture and status.
They must be tenant-qualified, rate-limited where applicable, and restricted to applicant-safe information.
Public responses must not expose duplicate evidence, internal notes, staff-only scores, Finance internals, or protected family details.

| Public route suffix | Purpose |
|---|---|
| `/{tenantId}/portal` | Return safe school branding, help contacts, office hours, and supported languages |
| `/{tenantId}/status/{token}/communication-preferences` | Update verified guardian email, SMS, and WhatsApp preferences |
| `/{tenantId}/status/{token}/withdraw` | Withdraw an eligible application, release Enrollment-owned seat state, and record audit evidence |

The family OTP route accepts either an inquiry primary contact or the phone/email of an active,
authority-verified guardian with explicit portal access. Guardian authorization is tenant- and
applicant-scoped and is rechecked on each status, registration, document, exam, offer, event,
communication-preference, and withdrawal
action. Public event routes list only future published events with family self-scheduling enabled;
reserve and cancel operations use the verified applicant session and preserve server-authoritative
capacity and timeline evidence.

## Contract rule

Do not add a parallel curated contract tree for Student.
Add endpoint summaries here when Student API docs need more route detail.
Do not restore generated exports under `docs/`.
