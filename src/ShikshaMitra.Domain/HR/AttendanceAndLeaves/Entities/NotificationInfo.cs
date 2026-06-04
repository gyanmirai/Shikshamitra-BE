using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class NotificationInfo : AggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; }

	public string ReceiverName { get; set; } = string.Empty;

	public DateTimeOffset NotificationTime { get; set; }

	public string ReceiverRole { get; set; } = string.Empty;

	public EmployeeAttendanceStatusType NotificationType { get; set; }

	public bool IsDelivered { get; set; }
}
