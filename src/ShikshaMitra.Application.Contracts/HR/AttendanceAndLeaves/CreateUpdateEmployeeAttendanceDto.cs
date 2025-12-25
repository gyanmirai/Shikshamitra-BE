using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class CreateUpdateEmployeeAttendanceDto
{
	[Required] public Guid EmployeeId { get; set; }
	
	/// <summary>
	/// The date of attendance (no time component).
	/// Required for the unique constraint (TenantId, EmployeeId, AttendanceDate).
	/// </summary>
	[Required] public DateOnly AttendanceDate { get; set; }
	
	/// <summary>
	/// Education Medium Stage ID for scope filtering.
	/// If not provided, it will be auto-populated from the associated Employee's EducationMediumStageId.
	/// If provided, it must match the employee's EducationMediumStageId exactly.
	/// Nullable to support legacy records created before scope filtering was implemented.
	/// </summary>
	public Guid? EducationMediumStageId { get; set; }
	
	public DateTimeOffset? TimeIn { get; set; }
	public DateTimeOffset? TimeOut { get; set; }

	public EmployeeAttendanceStatusType StatusType { get; set; }
}