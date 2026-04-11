using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class AutomatedNotificationsSettings : AggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; }
	public EmployeeAttendanceStatusType EmployeeAttendanceStatusType { get; set; }
	public List<NotificationMethodType> NotificationMethods { get; set; }

	private AutomatedNotificationsSettings()
	{
	}

	internal AutomatedNotificationsSettings(
		Guid id,
		Guid tenantId,
		EmployeeAttendanceStatusType employeeAttendanceStatusType,
		List<NotificationMethodType> notificationMethods) : base(id)
	{
		TenantId = tenantId;
		EmployeeAttendanceStatusType = employeeAttendanceStatusType;
		NotificationMethods = notificationMethods;
	}
}