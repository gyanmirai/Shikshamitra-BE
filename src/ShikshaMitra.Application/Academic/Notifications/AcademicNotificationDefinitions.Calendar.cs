using System.Collections.Generic;
using ShikshaMitra.Communication.Notifications;
using static ShikshaMitra.Communication.Notifications.AcademicNotificationEventKeys;
using static ShikshaMitra.Communication.Notifications.NotificationDeliveryProfile;
using static ShikshaMitra.Communication.Notifications.NotificationTriggerType;

namespace ShikshaMitra.Academic.Notifications;

internal static partial class AcademicNotificationDefinitions
{
	private static IEnumerable<NotificationEventDefinition> Calendar() =>
	[
		Event(CalendarPublished, $"{Teacher},{Family}"),
		Event(CalendarEventChanged, $"{Teacher},{Family}", Important, Change, "date", "start", "end", "location"),
		Event(CalendarEventCancelled, $"{Teacher},{Family}", Important),
		Event(CalendarEventReminder, $"{Teacher},{Family}", Routine, Due),
		Event(CalendarHolidayChanged, $"{Teacher},{Family}", Important, Change, "startDate", "endDate", "scope"),
		Event(CalendarEmergencyClosureDeclared, $"{Teacher},{Family}", Important),
		Event(CalendarEmergencyClosureChanged, $"{Teacher},{Family}", Important, Change, "startDate", "endDate", "scope", "status")
	];
}
