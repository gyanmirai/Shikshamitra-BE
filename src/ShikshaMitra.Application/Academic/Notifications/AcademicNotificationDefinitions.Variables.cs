using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Communication.Notifications;
using static ShikshaMitra.Communication.Notifications.NotificationVariableSensitivity;
using static ShikshaMitra.Communication.Notifications.NotificationVariableType;

namespace ShikshaMitra.Academic.Notifications;

internal static partial class AcademicNotificationDefinitions
{
	private static IReadOnlyList<NotificationVariableDefinition> Variables(string family)
	{
		NotificationVariableDefinition[] common =
		[
			new("school_name", Text, true, Public),
			new("recipient_name", Text, true, Personal),
			new("source_link", Link, true, AuthenticatedLink),
			new("student_name", Text, false, Personal),
			new("class_name", Text, false, Public)
		];
		var specific = family switch
		{
			"ClassTeacher" or "Subject" or "Elective" => Fields("subject_name", "teacher_name", "group_name"),
			"Syllabus" => Fields("subject_name", "syllabus_title", "revision_summary"),
			"LessonPlan" or "Diary" or "Progress" => Fields("subject_name", "lesson_plan_title", "review_comment"),
			"Substitution" => Fields("subject_name", "period_name", "room_name", "teacher_name", "start_time", "end_time")
				.Append(new("session_date", Date, false, Public)),
			"Room" => Fields("subject_name", "period_name", "room_name", "teacher_name", "start_time", "end_time")
				.Append(new("session_date", Date, false, Public)),
			"Timetable" => Fields("subject_name", "period_name", "room_name", "teacher_name")
				.Append(new("session_date", Date, false, Public)),
			"Calendar" => Fields("event_title", "location", "change_summary")
				.Append(new("event_date", Date, false, Public)),
			"Homework" or "Assignment" or "Evaluation" => Fields("subject_name", "task_title")
				.Append(new("due_date", NotificationVariableType.DateTime, false, Public)),
			"Exam" or "Seating" or "ExamDuty" or "Marks" or "Result" => Fields("exam_name", "subject_name", "room_name")
				.Append(new("exam_date", NotificationVariableType.DateTime, false, Public))
				.Append(new("seat_number", Text, false, Personal)),
			"Student360" => Fields("message_text", "meeting_proposal"),
			"Report" => Fields("report_name", "export_format"),
			"Summary" => Fields("summary_period", "summary_text"),
			"Settings" => Fields("setting_name", "impact_summary"),
			_ => throw new ArgumentOutOfRangeException(nameof(family))
		};
		return Array.AsReadOnly(common.Concat(specific).ToArray());
	}

	private static IEnumerable<NotificationVariableDefinition> Fields(params string[] keys) =>
		keys.Select(key => new NotificationVariableDefinition(key, Text, false, Personal));
}
