---
module: academic
domain: api
content_type: Reference
status: canonical
updated_on: 2026-11-20
owner: backend-team
audience: humans-and-agents
---

# Use the Academic setup APIs

TL;DR: four authenticated endpoints expose school-wide visit status and year-specific advisory guide state; they do not save operational configuration forms.
Contracts live in `src/ShikshaMitra.Application.Contracts/Academic/Setup/AcademicSetupDtos.cs` and `src/ShikshaMitra.Application.Contracts/Academic/Setup/IAcademicSetupAppService.cs`.
Operational workspaces, lookups, paged histories and report reads are documented in [UI read contracts](./references/ui-read-performance.md).

## Endpoints

### Staff notification configuration

Routes use the base path `/api/academic/notifications` and an authenticated, active school account. The action suffixes below refer to `Academics.Notification`.

| Endpoint | Action | Result |
|---|---|---|
| `GET /catalogue` | View | Event schemas, audience options, source availability and channel configuration flags |
| `GET /rules` | View | Paged event rules at a school, year, board, medium, class or division scope, with the effective rule and inheritance marker |
| `PUT /rules/{eventKey}` | Add | Save an explicitly scoped rule and return its revision and concurrency stamp |
| `POST /rules/{eventKey}/preview` | Add + source access | Preview a proposed `rule` for the given `sourceType` and `sourceId` |
| `GET /manual/student-360-audience/{studentId}?eventKey={eventKey}` | View + Send + Dashboard View + student and division scope | Get selectable family relationships, the saved academic medium, the main-contact setup flag and the assigned class teacher when available |
| `POST /manual/preview` | View + Send + source access | Review a saved source audience, chosen channels and published templates |
| `POST /manual/send` | Send + source access | Confirm a `reviewToken` with an `idempotencyKey`. Returns 202 with `operationId` and `status` |
| `GET /operations/pending-approvals?afterId={id}` | View + Add + source read access | List up to ten pending operations for the current assigned approver. Use `nextAfterId` for the next page |
| `GET /operations/{id}/approval-review` | View + Add + assigned approver + source access | Read eligible counts, channel exclusions and synthetic samples for a pending approval |
| `POST /operations/{id}/approve` and `/{id}/reject` | Add + assigned approver + source access | Decide an Academic operation in `PendingApproval` |
| `GET /operations/{id}` | View + source read access | Read the status, timestamps, delivery counts and intent outcomes for an operation within the caller's scope |
| `GET /history` | View + source read access | Page delivery attempts, work awaiting an attempt, and exclusions for the authorized source |
| `POST /dispatches/{id}/retry` | Add + source read access | Queue a verified retry of this channel's definitively rejected attempt. Pass `expectedAttemptId` to bind the request to the history row. |
| `POST /operations/{id}/cancel` | Add + source read access | Stop unclaimed work and return cancelled intent and dispatch counts plus remaining in-flight work |
| `GET /templates` | View | Paged template headers for the selected `eventKey` and optional `academicMediumId` |
| `GET /templates/{id}` and `/{id}/history` | View | Current template and revision history |
| `GET /templates/{id}/preview/{revisionId}` | View | Render with synthetic values for review |
| `POST /templates` and `PUT /templates/{id}/draft` | Add | Create or edit a draft |
| `POST /templates/initialize-defaults` | Add + school-wide scope | Create missing English variant drafts for one `eventKey` |
| `GET /templates/options` | View + template scope | Page published, non-retired variant names, channels and languages for an event without reading message content |
| `POST /templates/{id}/test-send` | Add + template scope | Queue a synthetic test for a single verified account |
| `POST /templates/{id}/publish` and `/{id}/restore-default` | Add | Publish an immutable revision or create a fresh draft from the system default |
| `DELETE /templates/{id}/draft` | Delete | Delete the selected unused draft |

Scope IDs form a contiguous tenant-owned hierarchy. A rule applies to the selected scope and its descendants. The nearest rule takes precedence, including a disabled override. Page items without a rule return a null `effectiveRule`. New overrides omit the concurrency stamp. Updates use the stamp of the exact rule being edited. A stale rule or draft write returns 409.

Rules default to the primary guardian for family events. Other saved guardian relationships and the student account can be selected when the event supports them. Enabling a rule requires an available source and valid published template coverage for its audiences, channels and fallback language. The catalogue reports automatic rule readiness as `availability` and reviewed send readiness as `manualAvailability`. Channel flags report configuration status. WhatsApp also requires approval for the selected template revision before dispatch.

Rule preview uses the saved source audience within the proposed rule's scope. It reports eligible recipients after contact, preference, consent and timing checks, estimated ready deliveries per channel, and grouped exclusions. Ready delivery counts depend on published template coverage, channel setup, and current device or provider approval. Selected drafts can render fictional samples. The preview response omits recipient identities and destinations, and the request creates no rules, campaigns, or dispatches.

The Student Academic 360 audience response sets `primaryGuardianNotConfigured` when the authorized active student has no resolvable main contact. This is independent of the configured guardian's eligibility on individual channels. Student Records owns main-contact changes and their permissions.

Manual preview accepts `eventKey`, `sourceType`, `sourceId`, `selectedRecipients`, `channels`, and optional `templateId`. Recipient references must come from the authorized source roster. An empty selection uses the source roster, subject to scope checks for every included segment. The in-app channel is required. The optional template selects a compatible published recipient, channel, and language variant. An effective rule supplies family preferences where present. Otherwise, the family audience defaults to the primary guardian.

For `Academic.Student360.GuardianMessage` and `Academic.Student360.MeetingRequested`, select the saved student reference and optionally the meeting's class teacher reference. The request must identify at least one source recipient. Omit `selectedFamilyRelationships` to use the student's primary guardian, or send a nonempty subset of the authorized choices, using `father`, `mother`, `guardian` and `self`. The audience endpoint returns relationship identifiers and their primary status without contact destinations. In-app and push require an active linked account. The preview applies channel access, preferences and published template coverage. Confirmation and dispatch recheck the saved source and reviewed audience. A meeting request asks recipients to arrange a time with the school.

Homework Due manual review uses the published task and upcoming division deadlines. It requires Learning Tasks View and the same source scope used by Homework Evaluation and Feedback. The roster uses current enrollment and locked elective selections where required, and excludes completed submissions. The reminder opens the personal notification inbox after sign-in.

Homework Published rules run when staff publish the task. The publication and its notification intents commit together. The audience includes eligible learners in published divisions, including tasks with no submission required. Class subject teachers use class scope. Teachers with division assignments use the assigned published division. A class assignment takes precedence when the same teacher is also assigned to a division. The teacher template deadline is the earliest upcoming deadline in that teacher's assigned scope. The worker rechecks assignments before dispatch.

Assignment Published uses the saved division schedule ID as its source ID. Both staff publication paths and the scheduled publisher queue the operation when the division becomes visible. Division recipients come from the published division, and class subject teachers receive the first release in class scope. The notification and publication commit together. The worker rechecks the current roster and teacher assignments before dispatch, and the notice expires at that division's deadline. Staff source access requires Learning Tasks View and access to the assignment.

Homework and Assignment Feedback Released use the submission ID as the source ID. An enabled rule can queue one notification operation per submission when staff first make feedback visible by marking it Reviewed or approving a rubric evaluation. Saving scores or submitting a rubric for approval leaves the notification unqueued. The source checks the learner's current enrollment, division, elective selection and review status before dispatch. Templates receive availability context and an authenticated inbox link. Marks, remarks and feedback text stay in the learner record. Assignment feedback history requires Assessment and Feedback View plus access to the submission's saved division.

Homework and Assignment Revision Requested use the same submission source and history permissions. An enabled rule queues a notice when staff request revision through feedback and save a reason on submitted work. The operation is unique to the submitted attempt. A later submission can receive a new request. Status-only updates and rubric setup do not queue notices. The worker rechecks the request, submitted attempt, published division and learner eligibility before sending. Resubmission, final review or closing the task ends eligibility for the prior request. The notice links to the authenticated inbox. The reason stays in the learner record.

Changed and Withdrawn remain unavailable for Homework and Assignment. Editing is limited to drafts, and closing completes the published task workflow. Evaluation Grading Due requires a saved grading deadline before it can be enabled.

An enabled Homework Due rule uses the school time zone and working days to queue reminders for each published division deadline. The worker scans current and legacy schedules by school, then rechecks the exact division, deadline and eligible learners. A deadline change invalidates queued work for the old occurrence. The reminder expires at the deadline.

Assignment Due follows the same school-day timing. It reads the publication state and deadline of each saved assignment division. Only the assigned elective cohort is eligible where required, and submitted or excused work is excluded using submissions tied to that assignment. Manual review requires Assessment and Feedback View plus access to the assignment.

For both due events, each scan can add newly eligible learners to the same division and deadline operation until the deadline. The worker keeps previously captured recipients in history and applies the current rule and quiet hours to new recipients. Cancelling the operation also stops future additions to that occurrence.

Homework Overdue and Assignment Overdue use the first missed deadline for an open, published division. An enabled rule can queue one operation per saved division deadline, with a 24-hour window for preparation and delivery. A saved publication timestamp must be at or before the deadline. The worker rechecks current enrollment, locked elective choices and submissions, and suppresses work for an expired, moved or closed deadline. Family delivery uses the configured guardian and student-account policy. An assigned teacher receives one summary for that division deadline. The optional `missing_submission_count` template variable reflects the individual learner's missing submission for their family and the division's remaining count when the teacher summary is prepared.

Homework Submitted and Assignment Submitted summarize the latest closed daily window for a published task and division. The effective rule's `timing.reminderLocalTime` is the cutoff in the school's time zone and defaults to 17:00. A submission at the cutoff belongs to the next window. The worker uses saved submission timestamps, counts distinct current learners with valid submissions, and resolves the assigned teachers for that division. The digest expires at the next cutoff. Its optional `submitted_count` variable contains a count without learner names, answers, grades or attachments.

The response reports recipient counts, exclusions, sample messages with fictional values, and whether the audience or a school approval rule requires approval. When at least one delivery is ready, it returns an opaque `reviewToken` and `expiresAt` up to ten minutes away, capped by source expiry. The server stores a digest bound to the actor, source revision, scoped audience, templates, and delivery evidence. Preview writes only the review receipt and makes no provider calls.

Confirmation opens the protected request attached to the token and checks the live source and reviewed delivery details. A material change, expired token, or reused token with a different key returns 409. Repeating the same token and key returns the existing operation. A `Queued` operation is awaiting delivery. The worker checks the reviewed delivery fingerprint again before creating dispatches.

Approval-required operations write no sendable intents until approval. Configure an active independent staff account at `Notifications:Academic:Tenants:<tenantIdN>:ApproverUserId`, where `tenantIdN` is the tenant GUID without hyphens. The assigned account needs Academic Notification Add and access to the source. Confirmation returns `Communication:Notification:ApprovalSetupRequired` if that account is unavailable. View also allows the approver to find their assigned operations in history. The approval review uses the original digest to verify the current source, audience and templates. A mismatch returns `canApprove: false` and no message samples. The approval command checks the digest again before queuing. An approver with current source access and access to the saved scope can reject a stale audience. Rejection ends the pending operation as `Cancelled` and sets `approvalRejected` on its status response.

Rule changes, template publication, test sends, confirmation, approval, rejection, retry and cancellation write an action audit in the same transaction as the change. Audit data is limited to the actor, tenant, action, relevant identifiers, revisions, status and counts. Replays of completed actions reuse the original audit history.

### Personal notification preferences

The authenticated school account owns its preferences. The endpoints below are relative to `/api/communication/notifications/me` and use the same current identity and student link checks as the inbox. Preference responses use `Cache-Control: no-store`.

| Endpoint | Result |
|---|---|
| `GET /preferences/options` | Registered category keys and their supported channels, plus ID and display name for each currently eligible linked student |
| `GET /preferences` | Existing account and accessible student preferences |
| `PUT /preferences` | Save the selected category, optional student context, enabled channels, preferred language, and optional school-time quiet hours. An existing row requires its current concurrency stamp. |

Account channel restrictions apply to linked student deliveries as well. A student setting can narrow those channels further. Rule eligibility, channel consent and device registration govern actual delivery.

### Personal browser subscriptions

The authenticated school account can call `GET /api/communication/notifications/me/push/subscriptions/{id}` to read the current registration state. The response contains only `isEnabled` and uses `Cache-Control: no-store`. A missing registration or one owned by a different account or school returns `404`.

### Catalogue availability

Available events have a registered source and a policy for viewing delivery history. Scheduled events also require a registered scanner. Unavailable entries cannot be enabled or sent manually. The catalogue returns `availabilityDetail` with the known source dependency or the existing workflow that covers the notice. Available entries return null for this field.

| Unavailable events | Source needed for delivery |
|---|---|
| Elective preference opened and due | A saved preference window with opening and due dates |
| Lesson plan submission due | A saved submission deadline policy |
| Homework and assignment changed or withdrawn | A published edit or withdrawal workflow with its affected audience. The current close action retains the published work and its submissions. |
| Evaluation grading due | A saved grading deadline |
| Exam approval requested, approved or changes requested | A review workflow with saved decisions |
| Exam rescheduled or cancelled | Saved transitions and the affected audiences |
| Seating changed | An audience snapshot from the preceding release |
| Exam duty assigned, changed or reminder | A saved duty assignment and reminder workflow |
| Marks entry opened or due | A saved release and deadline policy |
| Result ready for review | A saved review transition |
| Result corrected | Published corrections already use the result publication event with correction wording |
| Student Academic 360 follow-up due | A saved, authorized follow-up workflow |

### Timetable notifications

Timetable saves can queue `Academic.Timetable.Published`, `Changed` or `Withdrawn` for their saved class and division. Publication requires a transition from draft to published. Withdrawal uses the last published schedule when it returns to draft. A published re-save compares day, period, time, slot type, break, subject, elective group, teacher and room. Reordered input and regenerated child IDs preserve the notification revision. A published teacher change propagated by Curriculum uses the same timetable change event. The existing validation and schedule update commit with capture.

The notification rule selects the channels and recipient kinds. Learner recipients require active enrollment and applicable locked elective choices. A change to one optional subject reaches its affected cohort, including eligible families whose former option was removed. Staff targets come from affected periods before and after the save. Former teachers receive a generic alert with no new class or schedule details. The current source revision, enrollment and school-year expiry are checked again for queued work. Timetable history requires both Academic Notification View and the existing Timetable Management View and record scope.

`Academic.Timetable.DayReminder` is an automatic rule for upcoming teaching periods in a published timetable. It uses the rule's school-local cutoff and preceding-school-day offset, with the published calendar and lecture weekdays for the class and division. Existing published timetables are eligible for future reminders. The rule can select assigned teachers and verified student accounts through the Student account option. Per-recipient template fields give the school-local session date, period count and period times for the teacher's own assignments or the student's own timetable and locked elective choices.

The scheduler captures one operation per division, date and published schedule revision. A date with no periods or an active calendar closure is skipped. Queued work rechecks the schedule revision, closure, roster, rule and preferences, and expires at the first teaching period. In-app, email and Web Push are available through the existing channel settings.

### Room reservation notifications

The reservation workflow queues `Academic.Room.ApprovalRequested`, `Approved`, `Rejected`, `Changed` or `Cancelled` with the committed booking revision. Pending approval goes to the tenant's configured `RoomReviewerUserId` under `Notifications:Academic:Tenants:<tenant-id-without-hyphens>`. The reviewer must have an active school account, Timetable Management View, and current access to view and decide that request. A new decision or a change to the configured reviewer supersedes queued review work.

Decisions target the requester. A change to a confirmed booking can target the requester and the affected assigned teachers. Dates, times, rooms and assignment or scope changes are material. Notes alone leave the revision unchanged. The former teacher's notice has generic content. Booking notes and decision reasons stay in Room Availability. The source checks the current revision and school-local booking window before delivery.

A series-wide decision or cancellation queues one notice for the parent. A closure can also cancel an individual confirmed occurrence through the existing calendar impact handler. Delivery history requires Academic Notification View, Timetable Management View and visibility of the booking in Room Availability.

### Exam timetable notifications

The successful Pending-to-Published transition in `PUT /api/academics/exams/{examId}/status` can queue `Academic.Exam.TimetablePublished` in the same transaction. A repeated save of Published preserves the revision. The saved class, division, published subject and locked elective choice identify learners. The class subject and division teaching allocations identify assigned staff. The rule determines the actual family recipients and channels.

`Academic.Exam.Reminder` is automatic. A bounded scan uses upcoming published sittings and groups papers at the same start time in a division. The reminder uses the school rule's cutoff and preceding-school-day setting and expires at the sitting start. Recipient variables include the start time of their own applicable sitting. A calendar closure, changed enrollment, or a sitting that is no longer published or current can suppress pending delivery. Existing published exams can receive future reminders.

History requires Academic Notification View, Schedule Exam View and the source exam's existing access check. The current exam lifecycle locks published schedules. Reschedule, cancel, approval and invigilation events require their own saved workflows.

### Exam seating notifications

Either seating publish endpoint can queue `Academic.Seating.Published` once for the examination seating publication. Both the exam and the seating must be published. If seating is published first, the exam status action captures the notice from the saved options when the exam becomes visible. Both release paths coordinate in their publication transaction. Earlier seating records without saved notification options remain historical.

Publish options narrow the enabled rule. Parents permits the configured primary and additional guardians, Students permits a configured student account, and Teachers permits assigned teachers of the affected subjects. Email requires the saved Send Email choice. A future Schedule At value defers automatic delivery and the normal timing and quiet hours still apply. There is no invigilator relationship for the Exam Staff choice.

An eligible learner needs a current enrolled placement, a visible published subject and an actual seat in a published plan for that subject. Later calendar closures and eligibility changes suppress pending delivery. Templates use an authenticated inbox link. The optional `seat_number` belongs to the one learner and is available only when their applicable sessions all have the same room and seat. Staff and starter templates use generic content. History requires Academic Notification View, Seat Allocation View and existing source exam access. Seating Changed remains unavailable while published seating is immutable.

### Class teacher assignment notifications

Confirmed saves through `PUT /api/class-management/class-teacher-assignments` can queue `Academic.ClassTeacher.Assigned`, `Academic.ClassTeacher.Replaced` or `Academic.ClassTeacher.Removed`. The source ID is the affected division. The save transaction records the confirmed teacher and its notification revision on that division. Draft saves preserve the last confirmed identity and queue no notices. Reconfirming the same teacher preserves the revision even when the assignment row is recreated.

The rule can select the current or outgoing teacher where applicable. Each recipient must still have the saved active school account linked to an active teaching employee. The outgoing teacher receives generic content. The configured reviewer under `Notifications:Academic:Tenants:<tenantIdN>:ClassTeacherReviewerUserId` must have Class Teacher Assignment View and Add and scope for the division. A newer confirmed change or a conflicting live assignment suppresses pending work.

History requires Academic Notification View, Class Teacher Assignment View and the division scope. Teachers open the authenticated school inbox. The older assignment endpoint remains outside this notification workflow.

### Subject allocation notifications

An enabled `Academic.Subject.Allocated` rule can run when the existing Curriculum actions first publish a class subject. An enabled `Academic.Subject.TeacherChanged` rule can run when editing a published allocation changes an effective teacher binding. A repeated publication, unchanged binding or draft edit preserves notification history. The class-level audience uses the saved assigned teachers. A configured class-teacher copy uses each actual active division assignment. The outgoing teacher receives generic content.

An enabled `Academic.Subject.Withdrawn` rule can run when a published class subject becomes unpublished through its status, archive or removal workflow. Family audiences use active enrollment in the affected class and division. For an elective, a family qualifies only through the learner's locked choice for the removed subject. The source revision suppresses pending work after restoration or republication. Publication and withdrawal capture use the existing Subject Assignment permissions and source scope. The history additionally requires Academic Notification View and Subject Assignment View.

For a class-level action, the notification source ID is the class subject allocation. `PUT /api/class-management/teacher-assignments` uses the saved division-subject allocation ID when a teacher set changes for a published subject. An explicit empty list clears the assigned teachers. An unchanged teacher set leaves the current notice intact. The HTTP endpoint rejects a null teacher list. Subject Assignment Add and the selected division scope are required to capture a notice. The configured audience can include newly assigned teachers, removed teachers and the active class teacher for that division. Removed teachers receive generic content. A later edit or publication change invalidates pending work for the earlier revision. History requires Academic Notification View, Subject Assignment View and access to the affected division.

### Syllabus notifications

An enabled `Academic.Syllabus.Published` rule can queue work when `POST /api/academic/curriculum/chapters` or `PUT /api/academic/curriculum/chapters/{id}` saves a published chapter with learner or parent visibility on a published class subject. A material change to an already published chapter can queue `Academic.Syllabus.Revised`. Title, visible content, dates, attached documents and audience changes can produce a new revision. Draft saves, unchanged content and progress metadata stay silent. A withdrawn, deleted or superseded chapter makes its previous pending revision ineligible.

Capture requires Chapter Management Add and access to the source class. The rule chooses assigned subject teachers and the family audience. Family delivery uses active enrollment in the affected division and a matching locked choice for electives. Parent visibility excludes student accounts even when the rule enables them. Templates can use the chapter and subject names with the authenticated inbox link. Teacher notes and attachment contents stay in the syllabus. History requires Academic Notification View, Chapter Management View and access to the source scope.

### Academic calendar publication notifications

`POST /api/academic/calendars/{id}/publish` can queue `Academic.Calendar.Published` when a calendar becomes published. The existing Calendar Management Edit permission and access to the calendar's medium are required for notification capture. The calendar and its notification revision commit together. Draft saves and repeat publication leave the notification queue unchanged.

The rule uses the calendar's saved academic medium and year. A missing year uses the year of the saved medium. Rules can select current subject teachers and the family audience within the affected divisions. Family eligibility uses active enrollment and the shared recipient policy. Templates can use the calendar title and start date and link to the authenticated inbox. Internal descriptions stay in Calendar Management.

An archived calendar, a different publication revision or a changed title or date range makes a pending notice ineligible. It also expires after the last calendar date in the school's time zone. Delivery History requires Academic Notification View, Calendar Management View and access to the calendar's medium and saved operation scope.

### Academic calendar event changes

`PUT /api/academic/calendar-days/{id}` can queue `Academic.Calendar.EventChanged` when an event on a published calendar changes its date, time, venue or audience. One edit can update multiple class rows. The notification retains the previous and revised scopes from that edit. Capture requires Calendar Management Edit and access to the affected scopes. Description-only changes and draft edits leave the queue unchanged.

The saved rule selects eligible staff and families within those scopes. Recipients who only belong to the previous scope can see the original event details and a notice that they no longer apply. The revised audience can see the new details. Pending delivery checks the calendar's publication revision and the current event rows. History requires Academic Notification View, Calendar Management View and access to the relevant operation scope.

`DELETE /api/academic/calendar-days/{id}` can queue `Academic.Calendar.EventCancelled` for an active event on a published calendar. Capture requires Calendar Management Delete and access to the deleted event's scope. A draft deletion stays silent. Deletion supersedes pending reschedules. The cancellation rechecks the saved deleted row, current recipients, calendar publication and the scheduled date in the school's time zone.

Material edits to a published holiday or vacation through `PUT /api/academic/calendar-days/{id}` can queue `Academic.Calendar.HolidayChanged`. A vacation range edit uses the date and class rows saved together. The previous audience receives its original dates and the revised audience receives the new dates. Templates can use `event_date` for the first date and `holiday_end_date` for the last date. Draft and description-only edits stay silent.

An enabled `Academic.Calendar.EventReminder` rule schedules reminders for active events on published calendars. The selected division's school days determine the configured reminder date and time. The reminder expires when the event starts, or when its school-local date begins for an all-day event. Cancellation, publication changes and material event edits invalidate earlier pending reminders. Templates can use `event_date` and `event_start`. The reminder runs through the scheduler.

### Weekly Academic summary

Enable `Academic.Summary.Weekly` at the desired Academic scope after publishing templates. The rule uses `timing.weeklySummaryDay` (Sunday = 0 through Saturday = 6) and `timing.reminderLocalTime` in the school time zone. The default cutoff is Friday at 17:00. A saved, enabled rule is required.

The summary counts recorded submissions for published homework and assignments in the preceding seven-day window. It applies the current division, enrollment, and locked elective roster. Each family message uses its own child's counts. The primary guardian is the default. Additional relationships and student accounts follow the rule and require an active link to that child. Family email also requires the verified address on that linked account. Assigned class teachers and the configured reviewer receive division totals with Assessment and Feedback View and division access. The reviewer is configured through `Notifications:Academic:Tenants:{tenantId}:WeeklySummaryReviewerUserId`.

Each division can queue one summary per school-local calendar week. Empty weeks create no operation. A schedule edit preserves an already captured window. The delivery window closes at the same local time the following day. The event supports inbox, browser push and email. Templates can use `summary_period`, `summary_text`, `homework_submission_count` and `assignment_submission_count`. Push defaults to a generic message linking to the signed-in inbox.

### Weekly lecture requirements impact

An edit to Weekly Lectures can queue `Academic.Settings.OperationalImpact` for a published division timetable that matched the previous subject and elective lecture counts and now needs review. Save the enabled rule and publish the applicable staff templates for the timetable's scope. The same validation used during timetable publication determines whether the edit created a mismatch.

The audience is the currently scheduled teachers and the eligible reviewer configured through `Notifications:Academic:Tenants:{tenantIdN}:SettingsImpactReviewerUserId`. Each account requires Timetable Management View and access to the affected division. Delivery history requires Academic Notification View and the same timetable access. The event supports inbox and browser push. Templates can use `setting_name`, `impact_summary` and `class_name`.

Saving equivalent requirements or editing an already mismatched timetable's requirements keeps the same incident. Restoring the match, withdrawing the timetable or changing its saved schedule makes pending delivery ineligible.

### Report export notifications

The Academic export worker can queue `Academic.Report.ExportReady` or `Academic.Report.ExportFailed` when the current export attempt reaches its terminal state. These events use a school-wide rule and target only the saved requester. Interrupted and stale attempts queue no notice. A repeated terminal callback queues no duplicate.

The ready notice opens the authenticated report download route. It checks report ownership, completion and expiry on each request. Failure notices open the inbox. Templates can use `report_name` and `export_format`. Storage keys and worker diagnostics stay out of template data. Ready supports inbox, web push and email. Failure supports inbox and web push.

| Endpoint | Access | Result |
|---|---|---|
| `GET /api/app/academic-report/exports/{jobId}/recovery` | Original requester in the same school | Report status and whether the requester can generate it again |
| `POST /api/app/academic-report/exports/{jobId}/regenerate` | Original requester and the existing report export permission | Queue a replacement for an expired or missing file using the saved request and current data scope |

The replacement keeps the report identifier and the authenticated download link. Repeated requests while queued or running reuse that operation. Worker deliveries from the previous generation cannot claim the replacement. Dynamic exports apply the current scope and any narrower scope stored with the request.

### Emergency closure notifications

`POST /api/academic/calendar-days/emergency-closure` can queue `Academic.Calendar.EmergencyClosureDeclared` for each saved class or medium and date. `PUT /api/academic/calendar-days/{id}/emergency-closure` can queue `Academic.Calendar.EmergencyClosureChanged` for a date, time or scope change. `POST /api/academic/calendar-days/{id}/cancel-emergency-closure` can queue a reopening notice with the same change event. Capture requires the existing Emergency Closure Add, Edit or Delete permission for the corresponding action and access to the saved scope.

On declaration and material edits, the form's teacher, parent and student choices limit the enabled rule. Teachers come from current subject assignments. Family delivery uses active enrollment, the primary guardian default and the configured options for additional guardians and student accounts. A reopening uses the last saved notification choices. General staff delivery requires a scoped audience that this workflow does not currently provide.

The closure list supplies `notifyTeachers`, `notifyParents` and `notifyStudents` from the saved notification choices so the edit form restores them. Older closures without saved choices return all three as false. An audience-only edit saves the selections for future changes and can suppress pending delivery to people who were deselected. Newly selected groups become eligible on the next material change or reopening.

Recipients who belong only to the previous scope see the prior title and date and a notice that the closure no longer applies. The revised audience sees the current date and school-local time. Internal descriptions stay in the closure record. The queue rechecks the latest revision and saved closure status. Reopening suppresses the pending closure notice, and an archived calendar suppresses its notices. Quiet hours remain in effect unless the enabled rule explicitly allows bypass. History requires Academic Notification View, Emergency Closure View and the stored operation scope.

### Chapter progress digest

An enabled `Academic.Progress.BehindSchedule` rule can queue one digest per published subject and division for the completed school-local window. **Daily digest time** sets the cutoff. A chapter is eligible when its published start and end dates form a valid target, its end falls within the window and its latest division progress is below 100 percent. The scheduler reuses the occurrence on repeat scans, and the worker rechecks dates, progress, publication and staff before delivery.

The rule can select assigned subject teachers, the active class teacher, and an eligible reviewer configured at `Notifications:Academic:Tenants:<tenantIdN>:ProgressReviewerUserId`. The reviewer needs Chapter Management View and access to the division. The published template can use `chapter_count` with the class and subject and link to Chapter Management. Delivery History requires Academic Notification View, Chapter Management View and the stored division scope.

`Academic.Progress.MilestoneReached` uses the same cutoff and requires a saved whole percentage from 1 to 100. Each published chapter qualifies when its first recorded progress at or above that percentage falls in the window and its latest saved division progress still meets the percentage. The digest can select assigned subject teachers and the active class teacher. Templates can also use `milestone_percentage`. The worker rechecks current progress and the configured percentage. Audit remarks stay in Chapter Management.

### Teacher diary follow-up

An operator can manually send `Academic.Diary.FollowUpRequested` for a submitted diary attached to an approved lesson plan. Diary saves and submissions queue no notice. `GET /api/academic/notifications/manual/diary-follow-up-recipients/{diaryId}` returns the diary teacher's active staff reference and the configured lesson plan reviewer when eligible. It requires Academic Notification View and Send, Lesson Plan View, the saved division scope and existing diary read access. The configured reviewer must have the existing lesson plan review access.

Use the returned staff reference with the shared manual preview and confirmation endpoints. The diary revision and current staff eligibility are checked against the reviewed send. Templates can use the school, class, subject and lesson plan title and link to the authenticated inbox. Diary notes and learner observations stay in the diary.

### Lesson plan submission notifications

A submitted lesson plan can queue `Academic.LessonPlan.Submitted` from the existing create, submit, or bulk-submit action. The enabled rule sends to the active account configured at `Notifications:Academic:Tenants:<tenantIdN>:LessonPlanReviewerUserId`. The reviewer needs Lesson Plan View and Add, the source division scope, and eligibility under the existing review workflow. Drafts and submissions without an eligible configured reviewer queue no notice.

A bulk request groups plans from the same academic division into one notice. At dispatch, the worker rechecks current reviewer access and includes only the plans still awaiting review under the saved submission revision. The `lesson_plan_count` template variable reflects that current count. When one plan remains, the template can also use its title and subject. A change to the configured reviewer does not reroute earlier queued work. Submission reminders require an actual saved deadline.

### Lesson plan review notifications

The existing lesson plan `approve`, `request-changes` and `reject` commands can queue the corresponding Academic notification in the same transaction as the saved review. The rule targets the assigned plan owner through their active staff account. Repeated reviews in the same status preserve the original notice. The worker checks the saved review revision, current owner and class and division before delivery. A later revision makes the earlier notice ineligible.

Templates can use the lesson plan title, subject and class and include an authenticated school inbox link. Review remarks stay in the lesson plan. History requires Academic Notification View, Lesson Plan View and access to the source division.

### Marks review notifications

An enabled rule can queue `Academic.Marks.Submitted` after `POST /api/academics/exams/{examId}/marks/submit` moves the saved subject and division workflow to Submitted. The recipient is the school's active reviewer configured through `Notifications:Academic:Tenants:<tenantIdN>:MarksReviewerUserId`. The correction action at `POST /api/academics/exams/{examId}/marks/request-changes` can queue `Academic.Marks.ChangesRequested` for the subject's current class and division teaching assignments. Recipients need Record Marks View and Add and access to the source workflow. The actor's own notice is suppressed.

The transition and capture commit together. The saved workflow revision and status invalidate earlier pending notices after a correction, resubmission or verification. The queue rechecks the current reviewer, staff assignments, account status and record access. Draft and bulk saves, verification and publication of marks do not queue either review event. Marks entry opening and reminders require a saved entry window.

Templates carry the exam and subject names with an authenticated Record Marks link. Scores, notes and individual feedback stay in Record Marks. Delivery history requires Academic Notification View, Record Marks View and source access.

### Result publication notifications

The first successful `POST /api/academics/results/publish` can queue `Academic.Result.Published` in the publication transaction. The exam must be visible to learners and the result must have a published revision for the saved exam and division. The source uses released result rows and current enrollment. Withheld rows and students outside the division are excluded. The school's family rule and the shared guardian and student-account checks determine delivery.

The notice uses generic result-availability content and the authenticated school inbox link. Scores, grades, ranks, pass status and report cards stay in the authorized result views. Delivery history requires Academic Notification View, Result Creation View and current exam access. Earlier publication audit history distinguishes the first publication from a later correction release.

Substitution creation and assignment use the saved Send notification choice. With an enabled rule, a checked choice queues the assignment for the substitute and any configured staff audience. The unchecked choice skips automatic assignment and acknowledgement reminders. The existing Send reminder action queues a notice for an open duty awaiting acknowledgement when a reminder rule covers the duty. An explicitly queued reminder and the school-day schedule share one operation for the current duty and period end. A response, changed assignment, cancellation or expired period makes earlier reminder work ineligible. Recipient resolution uses saved staff identities and the duty's division. Templates receive the class, subject, period and date. Staffing notes stay in the duty record.

Substitution reads report Queued for active delivery work and Sent after recorded provider acceptance. The substitute acknowledgement status changes independently when the assigned teacher responds. An indeterminate provider response remains Unknown in the delivery status and history. A reminder request without eligible delivery work returns Not Sent. History requires Timetable Management View and access to the duty in addition to Academic Notification View.

The assignment rule can include a reviewer configured through `Notifications:Academic:Tenants:<tenantIdN>:SubstitutionReviewerUserId`. The reviewer needs access to the duty's class and division. Acknowledgement reminders target the substitute teacher first. To enable escalation, select both `SubstituteTeacher` and `ConfiguredReviewer` and supply `timing.reviewerEscalationMinutes` from 1 to 10,080. Existing rules without this setting target only the substitute.

The escalation delay starts at the first recorded acceptance of the current teacher reminder. It has a separate deduplication identity for the same duty revision and period end, uses the configured channels and quiet hours, and skips a second application of the rule's initial delay. A delay edit applies to future escalation work. The queued alert still requires a distinct reviewer with current scope. A response, changed duty, disabled escalation or expired period makes pending escalation ineligible. Unconfirmed acceptance of a teacher reminder does not start the escalation timer.

An effective change to an active substitution can queue `Academic.Substitution.Changed` when the duty has Send notification enabled and a change rule covers it. Repeating the same assignment and editing internal notes do not queue another change. For a reassignment, a configured previous-substitute variant receives the former teacher's own date, period and time. The current class, subject, room and staffing notes stay out of that variant. Later edits supersede pending notices for the earlier revision.

The first cancellation can queue `Academic.Substitution.Cancelled` before the duty ends. The configured audience is limited to staff whose earlier duty notice has recorded provider acceptance and whose role or prior duty still qualifies. A pending or unconfirmed notice supplies no cancellation eligibility. An informed former substitute remains eligible if the removal notice has yet to be accepted. A cancellation invalidates the pending duty notices and acknowledgement reminders.

Template test-send accepts only `revisionId` and `concurrencyStamp`. It renders sample values, queues one delivery, expires after ten minutes and returns 202 with `isSynthetic: true`. In-app, email and web push use the authenticated account by default. To designate a test account, set `Notifications:Academic:Tenants:<tenantIdN>:TemplateTest:<channel>:UserId`. WhatsApp requires a designated account with a verified number, current consent and a published revision with an approved provider binding. Email uses the account's verified address and the existing provider's consent policy. Web push uses an active subscription for that account. The test-send limit is five queued test messages per school and channel in 15 minutes.

`GET /api/communication/notifications/providers/whatsapp/templates/context?templateId={templateId}&revisionId={revisionId}` reads the school's resolved WhatsApp provider setting ID and the binding for the exact published revision. Read requires Provider Settings View and the template's Academic View permission and scope. `bindingMatchesProvider` reflects whether the binding was created for the currently configured provider account. Linking, submitting and syncing use the existing Provider Settings Add and template write checks. The context response omits provider account configuration and credentials.

Template test status and history are available to the staff member who queued the test while they have Notification View and access to the template. Read the operation using the returned ID, then pass that ID and its event, template ID and `Academic.NotificationTemplateTest` source type to history. These operations expose delivery evidence without recipient destinations or message content.

History takes `eventKey`, `sourceType`, `sourceId`, optional `operationId` and `state`, and standard `skipCount` and `maxResultCount` pagination. Staff see rows for their authorized source scopes. Whole-operation status and exclusions recorded before dispatch require access to every scope represented by that operation.

History keeps provider acceptance, delivery, read, failure, skips and unknown acceptance distinct. It returns recipient references, timestamps, and recognized reason codes. Message bodies, destinations and raw provider references or errors stay private.

A retry uses the original recipient and template. History returns `canRequestRetry` for a caller with Add on the current attempt when the saved evidence shows a recognized transient rejection with no provider acceptance and the operation and attempt still permit retry. The retry request rechecks source scope, eligibility, consent, expiry and the provider route before queueing. It honors any saved retry window and allows one attempt beyond the automatic limit. A repeat request for the same queued attempt returns the existing retry. A changed attempt, ambiguous outcome, or changed delivery condition returns 409 with `Communication:Notification:RetryRequiresReview`.

Cancellation applies to unclaimed pending dispatches, unleased intents awaiting planning, and operations awaiting approval. It preserves earlier attempts and reports any claimed work still in flight. Both commands record the actor, operation and reason in the audit log.

Initializing defaults preserves previous edits and deletions and creates drafts without activating a rule.

### Academic Insights

`GET /api/academic/dashboard/insights` accepts required non-empty GUIDs `academicYearId`, `boardId`, and `educationMediumId`, plus optional `standardId`, `divisionId`, and `studentId`.
It returns `role`, `scopeLabel`, UTC `generatedAt`, `sections`, and an authorized `students` selector list for personal accounts.
Each section contains `key`, `status` (`ready`, `empty`, `unavailable`, or `error`), `summary`, numeric nullable `metrics`, named chart `series`, and bounded `items`.
Points provide `key`, `label`, numeric `value`, optional numeric `x`/`y`, and optional `status`.

Staff reads require the Academic Dashboard View action, an active current-tenant identity, a matching year/board/medium hierarchy, and server-resolved UserScope.
Every aggregate additionally requires its actual feature View action before querying that source.
An independently permitted sibling workflow can return a navigation-only `unavailable` section with no metrics or records; otherwise denied sections are omitted.
Technical source errors are logged and return an `error` section without discarding other available sections. Business, identity, authorization, context, validation, and cancellation failures propagate.
Role names only choose presentation order. Active class teacher assignments determine the class teacher presentation label.
Missing scope and foreign hierarchy selections fail closed; `Academic:Insights:InvalidContext` identifies an inconsistent selected hierarchy.

| Section | Source and contract |
|---|---|
| `classrooms` | Active class teacher mappings; `class-mapping` preview is limited to 120 divisions |
| `student360` | Scoped enrolled students; `attendance-score` matches credit-aware submitted attendance in the last 30 days with published/frozen, non-withheld exam results; 60 points maximum; student totals are independent of that preview |
| `calendar` | Selected-context calendar entries applying to authorized classes/divisions or the whole calendar; generated weekend holidays are excluded before counting and selecting the next five by date/ID. Actual events and other holidays on weekend dates remain eligible. |
| `curriculum` | Latest recorded chapter/division progress (creation time, then ID tie-break), joined through tenant-owned chapters and assigned subjects; `recorded-progress` is 0-100 percent, with no invented planned target; 30 chart subjects maximum |
| `lesson` | Lesson plan counts grouped by review state in authorized year/divisions, limited by the owning service's own/reporting-teacher resolution and exact active class teacher divisions; only server-resolved administrator scope bypasses that ownership filter |
| `timetable` | Slot counts grouped by day and sequence; `periods.x` uses .NET day numbering (Monday 1 through Saturday 6), `y` is sequence; this is not a conflict count |
| `tasks` | Parent aggregates first pass the owning homework and assignment access policies, including all-target division rules and established homework subject ownership; published schedules then group into `overdue`, `today`, and `upcoming` due-date buckets |
| `evaluation` | Received submissions from the same authorized parents grouped by recorded review/grade timestamp; `no-recorded-review` does not imply evaluation is required or pending |
| `exams` | Exam stage counts whose class and assigned divisions match the selected context |
| `reports` | The caller's latest 100 completed exports, restricted to permitted report families and exact stored dynamic year/board/medium; legacy jobs without verifiable context are excluded |
| `notifications` | Four existing read-only browser template categories; no fabricated template counts, save operation, or automatic sending |
| `audits` | Existing academic audit service with exact selected year/medium; total plus 10 recent items |
| `settings` | The exact nine setup setting existence checks, filtered by each setting's View permission; class-specific checks require a selected class |

The existing report-job and audit sources cannot establish a class/division authorization boundary. Those two sections return `unavailable` for narrower scopes.
Setting existence does not establish operational readiness.
No source read changes data, grants, seed state, or schema.

Accounts with exactly one persisted `Student` or `Parent` role follow the existing portal-only account definition and use verified portal association/mobile placement authorization, even when explicitly granted Dashboard View.
Role selection never authorizes student data by itself. Mixed staff/parent accounts retain the staff path and require Dashboard View; a missing staff grant never falls back to child data.
Their response contains only personal attendance/results, curriculum, learning tasks, exams, calendar, and seven-day timetable data already available through those services.
Multiple linked students without a selection return `scopeLabel: "Select a student"`, the authorized selector list, and no sections.
A selected student must belong to that persisted association and exactly match the requested context; no module grant is inferred from a personal association.
Personal preview limits and date windows are stated in section summaries.
The personal calendar reads the verified full month before excluding generated weekend holidays and selecting five upcoming entries; it does not re-filter an already truncated dashboard preview.

| Method and route | Input | Result |
|---|---|---|
| `GET /api/academics/setup/visit-status` | None | `hasVisitedConfiguration`, `canAccessSetup`, nullable `firstVisitedAt` |
| `POST /api/academics/setup/visit` | Optional `settingKey` in a JSON object | `firstVisit`, `hasVisitedConfiguration`, nullable `firstVisitedAt` |
| `GET /api/academics/setup/{academicYearId}` | Optional query `academicMediumId`, `standardId` | Guide state |
| `PUT /api/academics/setup/{academicYearId}/progress` | Progress JSON below | Updated guide state |

Visit-status GET is read-only.
For an active school account with no eligible configuration View action, it returns `canAccessSetup: false`, `hasVisitedConfiguration: false` and `firstVisitedAt: null` without disclosing stored marker state.
A false visit value is authoritative for gating only when `canAccessSetup` is true.
POST accepts a missing or null `settingKey`, or `welcome`, when any eligible configuration View is granted.
A setting key requires that exact setting's View action.
Only the first successful writer receives `firstVisit: true`.
The stored actor identifier is never returned.

## Timetable conflict context

Teacher and room validation errors retain their existing codes and serialized `data.ConflictDetails` array. Each detail also includes nullable `ConflictingAcademicYearName`, `ConflictingEducationBoardName`, and `ConflictingEducationMediumName`, resolved from the competing class's server-owned hierarchy. The contextual `data.message` includes the same labels. Older records with missing hierarchy labels retain the existing class/division fallback. Conflict checks continue across boards and media within the current tenant.

## Room calendar requests

For dated room operations under `api/academics/timetable`, `room-availability` and `room-availability/schedule` accept an optional `academicMediumId` query parameter.
Reservation create/update, conflict-check and room-suggestion request bodies accept the same optional field.
Physical room occupation, maintenance, tenant isolation and reservation access checks apply independently of the calendar medium.
See [room calendar context](product.md#room-reservation-calendar-context) for calendar selection and saved-booking rules.

## Progress and response

```json
{
  "concurrencyStamp": "<current academic-year revision>",
  "currentStageKey": "curriculum",
  "currentSettingKey": "subjects",
  "deferredSettingKeys": ["elective-groups"],
  "finishReview": false,
  "academicMediumId": "<selected medium GUID>",
  "standardId": null
}
```

Progress accepts the six exact stage keys in [product.md](./product.md).
`currentSettingKey` may be null for any valid stage, including a stage with no visible settings.
A supplied setting must belong to its stage and be viewable by the caller.
Deferred keys must be unique, known, viewable and limited to nine.
`finishReview: true` is valid only at `review`; the server supplies review time and actor.
The request's context fields have the same scope validation as the GET query.

| State field | Meaning |
|---|---|
| `academicYearId`, `concurrencyStamp` | Tenant-owned year and current revision |
| `currentStageKey`, nullable `currentSettingKey` | Shared guide cursor; a hidden current setting is returned as null |
| `deferredSettingKeys` | Only keys the caller may view |
| nullable `reviewedAt` | Advisory guide review timestamp |
| `settings` | Nine entries with `key`, `canView`, `canManage`, `configurationState` |

`canManage` requires both View and Add for that setting.
The four configuration-state values are defined in [product.md](./product.md).
Responses contain no configuration counts or record identifiers.
A partial-permission save preserves deferred keys hidden from that caller.

## Exact keys and permissions

Each permission suffix below has the exact prefix `Academics.`.
View grants entry and status access; Add governs the existing configuration save operation and `canManage`.
The guide does not grant Add or call those operational save APIs.
Module visibility is separate from action authorization.

| Setting key | Stage | Permission suffix before `.View` or `.Add` |
|---|---|---|
| `subjects` | `curriculum` | `CurriculumManagement.Settings.SubjectCreation` |
| `elective-groups` | `curriculum` | `CurriculumManagement.Settings.ElectiveGroupCreation` |
| `weekly-lectures` | `timetable` | `Timetable.Settings.WeeklyLectures` |
| `lecture-slots` | `timetable` | `Timetable.Settings.LectureSlots` |
| `homework-types` | `homework` | `HomeworkAndAssignment.Settings.HomeworkTypes` |
| `assignment-types` | `homework` | `HomeworkAndAssignment.Settings.AssignmentTypes` |
| `rubrics` | `homework` | `HomeworkAndAssignment.Settings.RubricConfig` |
| `grading-scales` | `examination` | `Examination.Settings.GradeScaling` |
| `exam-types` | `examination` | `Examination.Settings.ExamTypes` |

## Student submission attachments

`GET /api/homework/submissions` and `GET /api/assignments/submissions` return `submissionAttachments` on each student row.
Each item contains `id`, `name`, `url`, `contentType`, and `size`; legacy single-file uploads may have null metadata.
The list maps every persisted mobile attachment, falls back to the legacy submitted file, and is empty for drafts or absent files.
URLs target the existing authenticated `GET /api/files/{fileName}` endpoint, which resolves storage within the current tenant.
Stored mobile blob paths must match the submission tenant before they are converted to file identifiers.
No new evaluation endpoint or migration is required.

## Security and context

Every endpoint requires an authenticated, active Identity account whose tenant agrees with the current server tenant.
Host context and forged cross-tenant identities are rejected.
Year state and progress require at least one eligible setting View and scope in the requested tenant-owned academic year.
A selected medium must belong to a board in that year and be within the caller's effective hierarchy.
A selected class requires a medium, must belong to that medium and must be accessible through the caller's exact class or applicable wildcard scope.
Narrow class access does not expose sibling-class timetable state.
Medium-level configuration can be inherited by an authorized child scope.
See the [canonical security standard](../../common/security/permission-consistency.md).

## Conflicts and errors

| Status or code | Meaning |
|---|---|
| 401 | Unauthenticated request |
| 403 | Inactive/mismatched school identity, missing View or inaccessible scope |
| 404 | Year, medium or class is absent from the required tenant/context relationship |
| 409 | Stale year revision or exhausted optimistic visit retry |
| `ShikshaMitra:AcademicSetup:Invalid` | Unknown key, invalid stage/setting pairing, duplicate deferred keys, missing medium for class or review outside its stage |
| `ShikshaMitra:AcademicSetup:InvalidStoredState` | Malformed, unsupported or invalid persisted metadata |

Validation and business errors return 400.
Visit conflicts retry with fresh units of work, up to three attempts, preserving the winning marker.
Progress conflicts require refetching the current year state before deciding whether to retry.
No request restores permissions, changes scope or resets school data.

## Elective confirmation notices

`POST /api/class-management/student-elective-assignments/lock` can queue an enabled `Academic.Elective.SelectionConfirmed` rule for each selection newly locked by the request. Repeating the lock, saving an editable selection or submitting a portal preference does not queue this notice. The notice uses the saved selection ID and revision. Capture requires Student Elective Assignment Add and access to the affected division. Delivery rechecks current enrollment, the published elective and the locked selection revision. The rule can notify the affected family and the teachers assigned to teach the elective. A later correction suppresses the pending confirmation. History requires Academic Notification View, Student Elective Assignment View and access to the division.

The current preference flow derives submission availability from published options and learner placement. It has no saved preference-window dates, so `Academic.Elective.PreferenceOpened` and `Academic.Elective.PreferenceDue` remain unavailable.

## Locked elective corrections

Both commands require `ShikshaMitraSubmodulePermissions.ClassManagement.StudentElectiveAssignment.Add` and current persisted membership of the tenant's protected `admin` role. Configured data-scope rules, including the separately configured administrator scope exception, remain enforced. The elective roster response supplies `canCorrectLockedAssignments`; client role claims do not authorize a correction.

| Endpoint | Contract |
|---|---|
| `POST /api/class-management/student-elective-assignments/corrections/preview` | `StudentElectiveCorrectionInput`: `academicMediumId`, `classId`, `divisionId`, `electiveGroupId`, `studentId`, `selectedSubjectId`, optional `preferredSubjectId` and `reason` (maximum 1,000 characters). Null preference retains the current preference. Returns old/new labels, all detected impacts, `reviewToken` and `expiresAt`. No mutation. |
| `POST /api/class-management/student-elective-assignments/corrections/confirm` | The identical input plus `reviewToken`. Revalidates admin membership, context, choices and related state in a serializable transaction; saves the locked selection and correction audit together. Returns 204. |

The protected token expires after ten minutes and binds the user, tenant, input and reviewed state. Invalid or expired tokens and changed impacts return `ClassManagement:ElectiveCorrectionReviewChanged`; refresh and review again. Invalid context/choices return `ClassManagement:InvalidElectiveCorrection`; unlocked rows return `ClassManagement:CorrectionRequiresLockedSelection`; unchanged choices return `ClassManagement:ElectiveCorrectionNoChanges`. These business errors return 400; authorization failure returns 403. Existing academic records are preserved as described in [product rules](product.md#data-integrity).

An enabled `Academic.Elective.LockedSelectionCorrected` rule can queue notices in the same transaction as a confirmed correction. The saved correction audit identifies the notice. Preview, stale review and replay produce no notice. Family delivery requires an active enrollment in the saved division, an unchanged locked selection and a published target elective. Staff recipients can include teachers assigned to the new and previous elective and the active class teacher for that division. The outgoing teacher receives generic copy. The reason and academic record contents remain in the authorized correction workflow. A newer edit to the selection suppresses the earlier pending notice. History requires Academic Notification View, Student Elective Assignment View and access to the saved division.
