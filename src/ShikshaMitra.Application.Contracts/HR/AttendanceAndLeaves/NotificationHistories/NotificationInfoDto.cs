using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.AttendanceAndLeaves.NotificationHistories;

public class NotificationInfoDto : EntityDto<Guid>
{
    public string ReceiverName { get; set; }
    public DateTimeOffset NotificationTime { get; set; }
    public string ReceiverRole { get; set; }
    public EmployeeAttendanceStatusType NotificationType { get; set; }
    public bool IsDelivered { get; set; }
}
