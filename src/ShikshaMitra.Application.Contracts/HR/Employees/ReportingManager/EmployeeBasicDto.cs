using System;

namespace ShikshaMitra.HR.Employees.ReportingManager;

public class EmployeeBasicDto
{
	public Guid Id { get; set; }
	public string Kind { get; set; } = "Employee";
	public string? Email { get; set; }
	public int? Status { get; set; }
	public string EmployeeCode { get; set; } = string.Empty;
	public string FullName { get; set; } = string.Empty;
	public string? Designation { get; set; }
}
