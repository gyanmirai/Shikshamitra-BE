using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class EmployeeAttendanceDto : EntityDto<Guid>
{
	public Guid EmployeeId { get; set; }

	public string EmployeeName { get; set; }

	/// <summary>
	/// Education Medium Stage ID for scope filtering.
	/// Nullable to support legacy records created before scope filtering was implemented.
	/// </summary>
	public Guid? EducationMediumStageId { get; set; }

	/// <summary>
	/// The date of attendance (no time component).
	/// </summary>
	public DateOnly AttendanceDate { get; set; }

	public DateTimeOffset? TimeIn { get; set; }
	public DateTimeOffset? TimeOut { get; set; }

	public double? TotalHoursWorked { get; set; }

	public EmployeeAttendanceStatusType StatusType { get; set; }
}