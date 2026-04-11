using System;
using System.Collections.Generic;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class AutomatedNotificationsSettingsDto
{
	public Guid Id { get; set; }
	public EmployeeAttendanceStatusType EmployeeAttendanceStatusType { get; set; }
	public ICollection<NotificationMethodType> NotificationMethods { get; set; }
}