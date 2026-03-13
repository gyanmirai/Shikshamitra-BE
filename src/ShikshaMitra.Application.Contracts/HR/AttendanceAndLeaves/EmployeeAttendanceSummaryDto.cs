using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class EmployeeAttendanceSummaryDto
{
	public Guid EmployeeId { get; set; }

	public string EmployeeName { get; set; }

	public int TotalWorkingDays { get; set; }

	public int PresentDays { get; set; }

	public int LeaveDays { get; set; }

	public int HalfDayLeaves { get; set; }

	public PagedResultDto<EmployeeAttendanceDto> EmployeeAttendances { get; set; }
}