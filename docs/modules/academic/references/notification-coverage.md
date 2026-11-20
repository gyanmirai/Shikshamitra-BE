---
module: academic
domain: notification-coverage
content_type: Reference
status: active
updated_on: 2026-11-20
owner: backend-team
audience: humans-and-agents
---

# Verify Academic notification events

The table maps each available event to its owning workflow and executable tests for recipient selection and the source transition, scheduled scan, or reviewed confirmation. The [catalogue availability contract](../api.md#catalogue-availability) documents the source dependency for each unavailable event.

The catalogue test checks the typed schema and link owner. The source coverage test matches the table to the live service registrations and records each source adapter and schema version. Automatic delivery requires an enabled rule and applicable published templates. The [rule and template checks](../testing.md#staff-notification-configuration-checks) cover the primary-guardian default, disabled rules and draft initialization for every template variant.

Run the [catalogue coverage command](../testing.md#notification-catalogue-coverage) to execute the named cases and save their results. Keep event IDs and method names in backticks so the command can match them exactly.

| Events | Owning workflow | Recipient and transition tests |
|---|---|---|
| `Academic.ClassTeacher.Assigned`, `Academic.ClassTeacher.Replaced`, `Academic.ClassTeacher.Removed` | [Class teacher assignment](../api.md#class-teacher-assignment-notifications) | `A_confirmed_class_teacher_replacement_uses_old_and_new_assignees_without_draft_notices` |
| `Academic.Subject.Allocated`, `Academic.Subject.TeacherChanged`, `Academic.Subject.Withdrawn` | [Subject allocation](../api.md#subject-allocation-notifications) | `Subject_allocation_notices_use_the_saved_publication_and_only_changed_teachers`, `Withdrawing_an_elective_notifies_only_the_enrolled_learners_with_the_locked_choice`, `Subject_publication_and_withdrawal_routes_queue_scoped_notices`, `Division_teacher_save_queues_only_the_changed_division` |
| `Academic.Syllabus.Published`, `Academic.Syllabus.Revised` | [Syllabus](../api.md#syllabus-notifications) | `Syllabus_only_notifies_for_the_saved_publication_and_material_revision`, `A_published_chapter_targets_the_matching_elective_and_honours_parent_visibility` |
| `Academic.Elective.SelectionConfirmed`, `Academic.Elective.LockedSelectionCorrected` | [Elective confirmation](../api.md#elective-confirmation-notices), [locked correction](../api.md#locked-elective-corrections) | `A_successful_lock_only_notifies_newly_confirmed_selections`, `Lock_and_correction_notify_only_the_saved_learner_and_assigned_staff` |
| `Academic.LessonPlan.Submitted` | [Lesson plan submission](../api.md#lesson-plan-submission-notifications) | `Submission_uses_the_configured_scoped_reviewer`, `Submission_and_review_use_the_scoped_reviewer_and_plan_owner` |
| `Academic.LessonPlan.Approved`, `Academic.LessonPlan.ChangesRequested`, `Academic.LessonPlan.Rejected` | [Lesson plan review](../api.md#lesson-plan-review-notifications) | `A_committed_review_notifies_the_plan_owner_once` |
| `Academic.Diary.FollowUpRequested` | [Teacher Diary follow-up](../api.md#teacher-diary-follow-up) | `A_submitted_diary_supports_reviewed_sends_to_its_exact_staff` |
| `Academic.Progress.BehindSchedule` | [Chapter progress digest](../api.md#chapter-progress-digest) | `Behind_schedule_scans_the_saved_scope_once_per_closed_window_and_needs_a_saved_target`, `Due_progress_is_queued_once_for_staff_in_the_saved_division` |
| `Academic.Progress.MilestoneReached` | [Chapter progress digest](../api.md#chapter-progress-digest) | `Only_the_first_crossing_of_a_configured_percentage_is_eligible`, `First_milestone_is_queued_once_and_rechecks_current_progress` |
| `Academic.Timetable.Published`, `Academic.Timetable.Changed`, `Academic.Timetable.Withdrawn`, `Academic.Timetable.DayReminder` | [Timetable](../api.md#timetable-notifications) | `Timetable_transitions_notify_only_affected_locked_elective_cohorts_and_teachers` |
| `Academic.Substitution.Assigned`, `Academic.Substitution.Changed`, `Academic.Substitution.Cancelled`, `Academic.Substitution.AcknowledgementDue` | [Substitution workflow](../api.md#timetable-notifications) | `Assignment_and_reminder_use_real_delivery_work_and_only_the_assigned_teacher` |
| `Academic.Room.ApprovalRequested`, `Academic.Room.Approved`, `Academic.Room.Rejected`, `Academic.Room.Changed`, `Academic.Room.Cancelled` | [Room reservations](../api.md#room-reservation-notifications) | `Confirmed_reservation_actions_queue_one_requester_notice_per_real_transition` |
| `Academic.Calendar.Published` | [Academic calendar publication](../api.md#academic-calendar-publication-notifications) | `Calendar_publication_queues_once_and_archiving_invalidates_the_saved_notice`, `Published_calendars_target_the_saved_division_and_archiving_invalidates_the_notice` |
| `Academic.Calendar.EventChanged`, `Academic.Calendar.HolidayChanged` | [Calendar event changes](../api.md#academic-calendar-event-changes) | `A_published_calendar_date_change_queues_for_the_saved_division_only`, `Published_calendars_target_the_saved_division_and_archiving_invalidates_the_notice` |
| `Academic.Calendar.EventCancelled` | [Calendar event changes](../api.md#academic-calendar-event-changes) | `Deleting_a_published_event_notifies_its_division_and_suppresses_the_pending_reschedule`, `Published_calendars_target_the_saved_division_and_archiving_invalidates_the_notice` |
| `Academic.Calendar.EventReminder` | [Calendar event reminder](../api.md#academic-calendar-event-changes) | `A_published_event_reminder_uses_the_preceding_open_school_day_and_rechecks_cancellation`, `Published_calendars_target_the_saved_division_and_archiving_invalidates_the_notice` |
| `Academic.Calendar.EmergencyClosureDeclared`, `Academic.Calendar.EmergencyClosureChanged` | [Emergency closures](../api.md#emergency-closure-notifications) | `Changing_a_class_closure_suppresses_the_prior_notice_and_notifies_its_scope`, `Published_calendars_target_the_saved_division_and_archiving_invalidates_the_notice` |
| `Academic.Homework.Published` | [Homework publication](../api.md#staff-notification-configuration) | `Homework_publication_captures_eligible_families_and_currently_assigned_teachers_once` |
| `Academic.Assignment.Published` | [Assignment publication](../api.md#staff-notification-configuration) | `Assignment_publication_only_captures_the_division_that_became_visible` |
| `Academic.Homework.Due` | [Homework reminders](../api.md#staff-notification-configuration) | `Scheduler_queues_one_real_homework_intent_and_rechecks_submissions_before_dispatch`, `Upcoming_homework_targets_only_saved_eligible_learners_and_rechecks_the_roster` |
| `Academic.Assignment.Due` | [Assignment reminders](../api.md#staff-notification-configuration) | `Assignment_due_uses_published_division_schedules_and_assignment_specific_submissions` |
| `Academic.Homework.Overdue`, `Academic.Assignment.Overdue` | [Overdue work](../api.md#staff-notification-configuration) | `Overdue_learning_task_queues_one_scoped_summary_and_rechecks_missing_work` |
| `Academic.Homework.Submitted`, `Academic.Assignment.Submitted` | [Submission digests](../api.md#staff-notification-configuration) | `Submission_digest_uses_one_school_local_window_and_one_scoped_teacher_summary` |
| `Academic.Homework.RevisionRequested`, `Academic.Homework.FeedbackReleased`, `Academic.Assignment.RevisionRequested`, `Academic.Assignment.FeedbackReleased` | [Revision requests and feedback](../api.md#staff-notification-configuration) | `Learning_task_feedback_only_notifies_the_affected_learner_after_review_or_rubric_approval` |
| `Academic.Exam.TimetablePublished`, `Academic.Exam.Reminder` | [Exam timetable](../api.md#exam-timetable-notifications) | `Publishing_an_exam_and_its_reminders_follow_the_released_subject_audience` |
| `Academic.Seating.Published` | [Exam seating](../api.md#exam-seating-notifications) | `Seating_notifications_use_both_releases_and_each_learners_own_seat`, `Student_only_scheduled_publication_excludes_guardian_and_email_templates`, `Seating_release_history_requires_access_to_seating` |
| `Academic.Marks.Submitted`, `Academic.Marks.ChangesRequested` | [Marks review](../api.md#marks-review-notifications) | `Marks_notifications_follow_review_transitions_and_keep_private_details_in_the_record`, `Marks_queue_for_current_staff_and_require_record_marks_history_access` |
| `Academic.Result.Published` | [Result publication](../api.md#result-publication-notifications) | `Real_result_publication_targets_the_eligible_roster_with_generic_content` |
| `Academic.Student360.GuardianMessage`, `Academic.Student360.MeetingRequested` | [Student Academic 360 reviewed sends](../api.md#staff-notification-configuration) | `Student_360_messages_and_meeting_requests_queue_only_the_reviewed_family_and_selected_teacher` |
| `Academic.Report.ExportReady`, `Academic.Report.ExportFailed` | [Report exports](../api.md#report-export-notifications) | `Only_the_current_terminal_export_notifies_the_saved_requester_once` |
| `Academic.Summary.Weekly` | [Weekly Academic summary](../api.md#weekly-academic-summary) | `The_weekly_capture_sends_child_totals_only_to_their_verified_family_and_separate_staff_aggregate` |
| `Academic.Settings.OperationalImpact` | [Weekly lecture requirements impact](../api.md#weekly-lecture-requirements-impact) | `Weekly_lecture_edits_notify_responsible_staff_once_when_a_published_timetable_stops_matching`, `Timetable_transitions_notify_only_affected_locked_elective_cohorts_and_teachers` |
