using System;

namespace ShikshaMitra.HR.Employees.ReportingManager;

public class EmployeeBasicDto
{
	public Guid Id { get; set; }
	public string EmployeeCode { get; set; } = string.Empty;
	public string FullName { get; set; } = string.Empty;
	public string? Designation { get; set; }
}
