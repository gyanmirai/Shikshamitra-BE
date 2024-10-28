using System.Collections;
using System.Collections.Generic;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class AutomatedNotificationsSettingsDto
{
	public EmployeeAttendanceStatusType EmployeeAttendanceStatusType { get; set; }
	public ICollection<NotificationMethodType> NotificationMethods { get; set; }
}