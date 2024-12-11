using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class CreateUpdateEmployeeAttendanceDto
{
	[Required] public Guid EmployeeId { get; set; }
	public DateTimeOffset? TimeIn { get; set; }
	public DateTimeOffset? TimeOut { get; set; }

	public EmployeeAttendanceStatusType StatusType { get; set; }
}