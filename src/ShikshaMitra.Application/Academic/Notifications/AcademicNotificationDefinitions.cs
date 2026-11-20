using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;

namespace ShikshaMitra.Academic.Notifications;

internal static partial class AcademicNotificationDefinitions
{
	internal static IReadOnlyList<NotificationEventDefinition> Create() => Array.AsReadOnly(
		Curriculum().Concat(Timetable()).Concat(Calendar()).Concat(LearningTasks())
			.Concat(Examination()).Concat(Operations()).ToArray());

	private static NotificationEventDefinition Event(string key, string selectors,
		NotificationDeliveryProfile profile = NotificationDeliveryProfile.Routine,
		NotificationTriggerType trigger = NotificationTriggerType.Change,
		params string[] materialFields)
	{
		var family = key.Split('.')[1];
		var source = $"Academic.{family}";
		var channels = profile is NotificationDeliveryProfile.Internal or NotificationDeliveryProfile.Digest
			? new[] { CommunicationChannelType.InApp, CommunicationChannelType.WebPush, CommunicationChannelType.Email }
			: new[] { CommunicationChannelType.InApp, CommunicationChannelType.WebPush, CommunicationChannelType.Email, CommunicationChannelType.WhatsApp };
		var variables = Variables(family);
		if (key is AcademicNotificationEventKeys.HomeworkOverdue or AcademicNotificationEventKeys.AssignmentOverdue)
			variables = Array.AsReadOnly(variables.Append(new NotificationVariableDefinition("missing_submission_count",
				NotificationVariableType.Number, false, NotificationVariableSensitivity.Personal)).ToArray());
		if (key is AcademicNotificationEventKeys.HomeworkSubmitted or AcademicNotificationEventKeys.AssignmentSubmitted)
			variables = Array.AsReadOnly(variables.Append(new NotificationVariableDefinition("submitted_count",
				NotificationVariableType.Number, false, NotificationVariableSensitivity.Personal)).ToArray());
		if (key == AcademicNotificationEventKeys.LessonPlanSubmitted)
			variables = Array.AsReadOnly(variables.Append(new NotificationVariableDefinition("lesson_plan_count",
				NotificationVariableType.Number, false, NotificationVariableSensitivity.Personal)).ToArray());
		if (key == AcademicNotificationEventKeys.ProgressMilestoneReached)
			variables = Array.AsReadOnly(variables.Append(new NotificationVariableDefinition("milestone_percentage",
				NotificationVariableType.Number, false, NotificationVariableSensitivity.Public)).ToArray());
		if (key == AcademicNotificationEventKeys.CalendarHolidayChanged)
			variables = Array.AsReadOnly(variables.Append(new NotificationVariableDefinition("holiday_end_date",
				NotificationVariableType.Date, false, NotificationVariableSensitivity.Public)).ToArray());
		if (key == AcademicNotificationEventKeys.CalendarEventReminder)
			variables = Array.AsReadOnly(variables.Append(new NotificationVariableDefinition("event_start",
				NotificationVariableType.DateTime, false, NotificationVariableSensitivity.Public)).ToArray());
		if (key == AcademicNotificationEventKeys.TimetableDayReminder)
			variables = Array.AsReadOnly(variables.Concat(new[]
			{
				new NotificationVariableDefinition("period_count", NotificationVariableType.Number, false, NotificationVariableSensitivity.Personal),
				new NotificationVariableDefinition("period_times", NotificationVariableType.Text, false, NotificationVariableSensitivity.Personal)
			}).ToArray());
		return new(key, family, source, source, 1, trigger, profile,
			Array.AsReadOnly(selectors.Split(',').Distinct(StringComparer.Ordinal).ToArray()),
			Array.AsReadOnly(channels), variables, Array.AsReadOnly(materialFields),
			key is "Academic.Calendar.EmergencyClosureDeclared" or "Academic.Calendar.EmergencyClosureChanged");
	}

	private const string Family = "PrimaryGuardian,AdditionalGuardians,StudentAccount";
	private const string Teacher = "AssignedTeacher";
	private const string Reviewer = "ConfiguredReviewer";
	private const string ClassTeacher = "ClassTeacher";
	private const string Invigilator = "Invigilator";
	private const NotificationDeliveryProfile StaffOnly = NotificationDeliveryProfile.Internal;
}
