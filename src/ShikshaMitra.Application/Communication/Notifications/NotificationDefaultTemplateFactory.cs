using System;
using System.Text.RegularExpressions;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Communication.Notifications;

public sealed record NotificationDefaultTemplateContent(string Subject, string Body);

public sealed partial class NotificationDefaultTemplateFactory : ITransientDependency
{
	public NotificationDefaultTemplateContent Create(NotificationEventDefinition definition, string recipientKind,
		CommunicationChannelType channel, string language)
	{
		if (!string.Equals(language, "en", StringComparison.OrdinalIgnoreCase)) throw new BusinessException(NotificationErrorCodes.TemplateInvalid);
		var action = definition.EventKey.Split('.')[^1];
		var title = Words().Replace(action, "$1 $2");
		var subject = definition.Category + ": " + title;
		var message = (recipientKind, definition.EventKey) switch
		{
			("AssignedTeacher", AcademicNotificationEventKeys.HomeworkOverdue or AcademicNotificationEventKeys.AssignmentOverdue) =>
				"{{task_title}} had {{missing_submission_count}} missing submissions when this summary was prepared.",
			("AssignedTeacher", AcademicNotificationEventKeys.HomeworkSubmitted or AcademicNotificationEventKeys.AssignmentSubmitted) =>
				"Submissions recorded for {{task_title}} during this digest window: {{submitted_count}}.",
			(_, AcademicNotificationEventKeys.HomeworkRevisionRequested or AcademicNotificationEventKeys.AssignmentRevisionRequested) =>
				"A revision was requested for {{task_title}}.",
			("Guardian" or "StudentAccount", AcademicNotificationEventKeys.TimetablePublished) =>
				"The timetable for {{class_name}} has been published.",
			("Guardian" or "StudentAccount", AcademicNotificationEventKeys.TimetableChanged) =>
				"A timetable period for {{class_name}} has changed.",
			("Guardian" or "StudentAccount", AcademicNotificationEventKeys.TimetableWithdrawn) =>
				"The timetable for {{class_name}} has been withdrawn.",
			(_, AcademicNotificationEventKeys.TimetablePublished) => "Your teaching timetable has been published.",
			(_, AcademicNotificationEventKeys.TimetableChanged) => "A period you were assigned has changed.",
			(_, AcademicNotificationEventKeys.TimetableWithdrawn) => "A timetable containing your assigned periods has been withdrawn.",
			(_, AcademicNotificationEventKeys.TimetableDayReminder) =>
				"Your timetable for {{session_date}} has {{period_count}} scheduled periods. Period times: {{period_times}}.",
			("SubstituteTeacher", AcademicNotificationEventKeys.SubstitutionAssigned) =>
				"You have a substitute duty for {{period_name}} on {{session_date}}.",
			("PreviousSubstitute", AcademicNotificationEventKeys.SubstitutionChanged) =>
				"You are no longer assigned to the substitute duty for {{period_name}} on {{session_date}}.",
			("SubstituteTeacher", AcademicNotificationEventKeys.SubstitutionChanged) =>
				"Your substitute duty is {{period_name}} on {{session_date}}, from {{start_time}} to {{end_time}}.",
			(_, AcademicNotificationEventKeys.SubstitutionChanged) =>
				"The substitute duty is now {{period_name}} on {{session_date}}, from {{start_time}} to {{end_time}}.",
			(_, AcademicNotificationEventKeys.SubstitutionCancelled) =>
				"A substitute duty was cancelled. Check your school notifications for updates.",
			("SubstituteTeacher", AcademicNotificationEventKeys.SubstitutionAcknowledgementDue) =>
				"Please acknowledge your substitute duty for {{period_name}} on {{session_date}}.",
			("ConfiguredReviewer", AcademicNotificationEventKeys.SubstitutionAcknowledgementDue) =>
				"The substitute duty for {{period_name}} on {{session_date}} still needs acknowledgement.",
			(_, AcademicNotificationEventKeys.RoomApprovalRequested) =>
				"A booking for {{room_name}} on {{session_date}} from {{start_time}} to {{end_time}} needs your decision.",
			(_, AcademicNotificationEventKeys.RoomApproved) =>
				"Your booking for {{room_name}} on {{session_date}} from {{start_time}} to {{end_time}} was approved.",
			(_, AcademicNotificationEventKeys.RoomRejected) =>
				"Your room booking for {{session_date}} was declined. Open Room Availability to see the decision.",
			("Requester", AcademicNotificationEventKeys.RoomChanged) =>
				"Your booking is now for {{room_name}} on {{session_date}} from {{start_time}} to {{end_time}}.",
			(_, AcademicNotificationEventKeys.RoomChanged) => "A room booking involving you changed.",
			(_, AcademicNotificationEventKeys.RoomCancelled) => "Your room booking for {{room_name}} on {{session_date}} was cancelled.",
			("PreviousTeacher", AcademicNotificationEventKeys.ClassTeacherReplaced or AcademicNotificationEventKeys.ClassTeacherRemoved) =>
				"Your class teacher assignment has ended.",
			("AssignedTeacher", AcademicNotificationEventKeys.ClassTeacherAssigned or AcademicNotificationEventKeys.ClassTeacherReplaced) =>
				"You have been assigned as class teacher for {{class_name}}.",
			(_, AcademicNotificationEventKeys.ClassTeacherAssigned or AcademicNotificationEventKeys.ClassTeacherReplaced or AcademicNotificationEventKeys.ClassTeacherRemoved) =>
				"The class teacher assignment for {{class_name}} has changed.",
			(_, AcademicNotificationEventKeys.SubjectAllocated) => "{{subject_name}} has been allocated for {{class_name}}.",
			("PreviousTeacher", AcademicNotificationEventKeys.SubjectTeacherChanged) => "Your teaching assignment has changed.",
			(_, AcademicNotificationEventKeys.SubjectTeacherChanged) => "The teaching assignment for {{subject_name}} in {{class_name}} has changed.",
			(_, AcademicNotificationEventKeys.SubjectWithdrawn) => "{{subject_name}} has been withdrawn for {{class_name}}.",
			(_, AcademicNotificationEventKeys.SyllabusPublished) => "A syllabus chapter for {{subject_name}} is available.",
			(_, AcademicNotificationEventKeys.SyllabusRevised) => "A published syllabus chapter for {{subject_name}} has been updated.",
			("PreviousTeacher", AcademicNotificationEventKeys.ElectiveLockedSelectionCorrected) => "An elective teaching assignment has changed.",
			(_, AcademicNotificationEventKeys.ElectiveLockedSelectionCorrected) => "A confirmed elective assignment has been updated.",
			(_, AcademicNotificationEventKeys.ElectiveSelectionConfirmed) => "An elective assignment has been confirmed.",
			(_, AcademicNotificationEventKeys.LessonPlanSubmitted) => "Lesson plans awaiting review: {{lesson_plan_count}}.",
			(_, AcademicNotificationEventKeys.LessonPlanApproved) => "Your lesson plan has been approved.",
			(_, AcademicNotificationEventKeys.LessonPlanChangesRequested) => "Changes have been requested for your lesson plan.",
			(_, AcademicNotificationEventKeys.LessonPlanRejected) => "Your lesson plan was reviewed and rejected.",
			(_, AcademicNotificationEventKeys.DiaryFollowUpRequested) => "A follow-up was requested for a submitted teacher diary.",
			(_, AcademicNotificationEventKeys.CalendarPublished) => "{{event_title}} is now available. Open your school notifications for details.",
			(_, AcademicNotificationEventKeys.CalendarEventChanged) => "{{event_title}} has changed. Check your school notifications for details.",
			(_, AcademicNotificationEventKeys.CalendarEmergencyClosureDeclared or AcademicNotificationEventKeys.CalendarEmergencyClosureChanged) =>
				"{{event_title}} on {{event_date}}. {{change_summary}}",
			(_, AcademicNotificationEventKeys.ProgressBehindSchedule) => "Review the incomplete chapters for {{subject_name}} in {{class_name}} ({{chapter_count}}).",
			(_, AcademicNotificationEventKeys.ProgressMilestoneReached) => "Review chapters that reached {{milestone_percentage}}% for {{subject_name}} in {{class_name}} ({{chapter_count}}).",
			(_, AcademicNotificationEventKeys.MarksSubmitted) => "Marks for {{subject_name}} in {{exam_name}} are ready for review.",
			(_, AcademicNotificationEventKeys.MarksChangesRequested) => "A correction was requested for {{subject_name}} in {{exam_name}}.",
			(_, AcademicNotificationEventKeys.ResultPublished) => "A result is available. Sign in to ShikshaMitra to view it.",
			(_, AcademicNotificationEventKeys.ExamTimetablePublished) => "The timetable for {{exam_name}} is available. Sign in to view your exam dates.",
			(_, AcademicNotificationEventKeys.ExamReminder) => "Your scheduled sitting for {{exam_name}} starts at {{exam_date}}.",
			("AssignedTeacher", AcademicNotificationEventKeys.SeatingPublished) => "Seating is available for an exam you teach. Sign in to view it.",
			(_, AcademicNotificationEventKeys.SeatingPublished) => "Your seating plan is available. Sign in to view your seat.",
			_ => subject + "."
		};
		var body = channel == CommunicationChannelType.WebPush ? "An academic update is available. Open the school website for details." :
			$"Hello {{{{recipient_name}}}},\n{message}\nView details: {{{{source_link}}}}\n{{{{school_name}}}}";
		if (channel == CommunicationChannelType.Email)
			body = $"<p>Hello {{{{recipient_name}}}},</p><p>{message}</p><p><a href=\"{{{{source_link}}}}\">View details</a></p><p>{{{{school_name}}}}</p>";
		return new(subject, body);
	}
	[GeneratedRegex("([a-z])([A-Z])", RegexOptions.CultureInvariant)]
	private static partial Regex Words();
}
