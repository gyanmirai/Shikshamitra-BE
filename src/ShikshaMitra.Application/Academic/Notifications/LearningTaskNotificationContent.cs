using System;
using System.Collections.Generic;
using System.Globalization;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Communication.Notifications;

namespace ShikshaMitra.Academic.Notifications;

internal static class LearningTaskNotificationContent
{
	internal const string Link = "/dashboard?openNotifications=1";

	internal static NotificationSourceScopeDto Scope(LearningTaskNotificationRecord record, Guid? divisionId = null) =>
		new(record.Placement.YearId, record.Placement.BoardId, record.Placement.MediumId, record.Task.StandardId, divisionId);

	internal static Dictionary<string, NotificationVariableValueDto> Variables(LearningTaskNotificationRecord record, string schoolName) =>
		new(StringComparer.Ordinal)
		{
			["school_name"] = Text(schoolName), ["class_name"] = Text(record.Placement.ClassName),
			["subject_name"] = Text(record.Subject.Name), ["task_title"] = Text(record.Task.Title),
		};

	internal static Dictionary<string, NotificationVariableValueDto> WithDeadline(
		Dictionary<string, NotificationVariableValueDto> common, DateTimeOffset dueAt) =>
		new(common, StringComparer.Ordinal)
		{
			["due_date"] = new(NotificationVariableType.DateTime,
				LearningTaskScheduleTimestamp.ToSchoolTime(dueAt.UtcDateTime).ToString("O", CultureInfo.InvariantCulture)),
		};

	internal static NotificationSourceSnapshot Unavailable() =>
		new(false, NotificationErrorCodes.SourceUnavailable, "", null, new Dictionary<string, NotificationVariableValueDto>(), []);

	private static NotificationVariableValueDto Text(string value) => new(NotificationVariableType.Text, value);
}
