using System.Collections.Generic;
using ShikshaMitra.Communication.Notifications;
using static ShikshaMitra.Communication.Notifications.AcademicNotificationEventKeys;
using static ShikshaMitra.Communication.Notifications.NotificationDeliveryProfile;
using static ShikshaMitra.Communication.Notifications.NotificationTriggerType;

namespace ShikshaMitra.Academic.Notifications;

internal static partial class AcademicNotificationDefinitions
{
	private static IEnumerable<NotificationEventDefinition> Timetable() =>
	[
		Event(TimetablePublished, $"{Teacher},{Family}"),
		Event(TimetableChanged, $"{Teacher},{Family}", Important, Change, "periods", "room", "teacher", "effectiveDate"),
		Event(TimetableWithdrawn, $"{Teacher},{Family}", Important),
		Event(TimetableDayReminder, $"{Teacher},StudentAccount", Digest, Due),
		Event(SubstitutionAssigned, $"SubstituteTeacher,OriginalTeacher,{Reviewer}", Important),
		Event(SubstitutionChanged, $"PreviousSubstitute,SubstituteTeacher,OriginalTeacher,{Reviewer}", Important,
			Change, "substitute", "date", "period", "room"),
		Event(SubstitutionCancelled, $"SubstituteTeacher,OriginalTeacher,{Reviewer}", Important),
		Event(SubstitutionAcknowledgementDue, "SubstituteTeacher", Important, Due),
		Event(RoomApprovalRequested, Reviewer, StaffOnly),
		Event(RoomApproved, "Requester"),
		Event(RoomRejected, "Requester"),
		Event(RoomChanged, $"Requester,{Teacher}", Important, Change, "date", "start", "end", "room"),
		Event(RoomCancelled, $"Requester,{Teacher}", Important)
	];
}
