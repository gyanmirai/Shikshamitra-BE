using System.Collections.Generic;
using ShikshaMitra.Communication.Notifications;
using static ShikshaMitra.Communication.Notifications.AcademicNotificationEventKeys;
using static ShikshaMitra.Communication.Notifications.NotificationDeliveryProfile;
using static ShikshaMitra.Communication.Notifications.NotificationTriggerType;

namespace ShikshaMitra.Academic.Notifications;

internal static partial class AcademicNotificationDefinitions
{
	private static IEnumerable<NotificationEventDefinition> Examination() =>
	[
		Event(ExamApprovalRequested, Reviewer, StaffOnly),
		Event(ExamApproved, $"{Teacher},{Reviewer}", StaffOnly),
		Event(ExamChangesRequested, $"{Teacher},{Reviewer}", StaffOnly),
		Event(ExamTimetablePublished, $"{Family},{Teacher}", Important),
		Event(ExamRescheduled, $"{Family},{Teacher},{Invigilator}", Important, Change, "date", "start", "end", "venue"),
		Event(ExamCancelled, $"{Family},{Teacher},{Invigilator}", Important),
		Event(ExamReminder, $"{Family},{Teacher}", Routine, Due),
		Event(SeatingPublished, $"{Family},{Invigilator}", Important),
		Event(SeatingChanged, $"{Family},{Invigilator}", Important, Change, "room", "seat", "invigilator"),
		Event(ExamDutyAssigned, Invigilator, Important),
		Event(ExamDutyChanged, $"PreviousInvigilator,{Invigilator}", Important, Change, "date", "period", "room", "status"),
		Event(ExamDutyReminder, Invigilator, Routine, Due),
		Event(MarksEntryOpened, Teacher, StaffOnly),
		Event(MarksEntryDue, $"{Teacher},{Reviewer}", StaffOnly, Due),
		Event(MarksSubmitted, Reviewer, StaffOnly),
		Event(MarksChangesRequested, Teacher, StaffOnly),
		Event(ResultReadyForReview, Reviewer, StaffOnly),
		Event(ResultPublished, Family, Private),
		Event(ResultCorrected, $"{Family},{Teacher},{Reviewer}", Private)
	];
}
