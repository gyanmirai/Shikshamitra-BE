using System.Collections.Generic;
using ShikshaMitra.Communication.Notifications;
using static ShikshaMitra.Communication.Notifications.AcademicNotificationEventKeys;
using static ShikshaMitra.Communication.Notifications.NotificationDeliveryProfile;
using static ShikshaMitra.Communication.Notifications.NotificationTriggerType;

namespace ShikshaMitra.Academic.Notifications;

internal static partial class AcademicNotificationDefinitions
{
	private static IEnumerable<NotificationEventDefinition> Operations() =>
	[
		Event(Student360GuardianMessage, Family, NotificationDeliveryProfile.Manual, NotificationTriggerType.Manual),
		Event(Student360MeetingRequested, $"{Family},{ClassTeacher}", NotificationDeliveryProfile.Manual, NotificationTriggerType.Manual),
		Event(Student360FollowUpDue, $"{ClassTeacher},{Reviewer}", StaffOnly, Due),
		Event(ReportExportReady, "Requester", Private),
		Event(ReportExportFailed, "Requester", StaffOnly),
		Event(SummaryWeekly, $"{ClassTeacher},{Reviewer},{Family}", Digest, Due),
		Event(SettingsOperationalImpact, $"{Reviewer},{Teacher}", StaffOnly)
	];
}
