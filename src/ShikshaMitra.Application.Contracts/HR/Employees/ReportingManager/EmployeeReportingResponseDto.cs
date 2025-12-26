using System;

namespace ShikshaMitra.HR.Employees.ReportingManager;

public class EmployeeReportingResponseDto
{
	public Guid EmployeeId { get; set; }
	public EmployeeBasicDto? ReportingManager { get; set; }
}
