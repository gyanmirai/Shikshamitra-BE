using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class EmployeeAttendanceDto : EntityDto<Guid>
{
	public Guid EmployeeId { get; set; }

	public string EmployeeName { get; set; }

	public DateTimeOffset? TimeIn { get; set; }
	public DateTimeOffset? TimeOut { get; set; }

	public double? TotalHoursWorked { get; set; }

	public EmployeeAttendanceStatusType StatusType { get; set; }
}