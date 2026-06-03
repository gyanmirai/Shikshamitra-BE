using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class AutomatedNotificationsSettingsDto : EntityDto<Guid>
{
	public EmployeeAttendanceStatusType EmployeeAttendanceStatusType { get; set; }
	public ICollection<NotificationMethodType> NotificationMethods { get; set; } = [];
}
