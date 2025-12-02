using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class CreateUpdateEmployeeAttendanceDto
{
	[Required] public Guid EmployeeId { get; set; }
	
	/// <summary>
	/// Education Medium Stage ID for scope filtering.
	/// Must match the employee's EducationMediumStageId.
	/// </summary>
	[Required] public Guid EducationMediumStageId { get; set; }
	
	public DateTimeOffset? TimeIn { get; set; }
	public DateTimeOffset? TimeOut { get; set; }

	public EmployeeAttendanceStatusType StatusType { get; set; }
}