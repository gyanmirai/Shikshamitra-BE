using System;

namespace ShikshaMitra.HR.Employees.ReportingManager;

public class EmployeeBasicDto
{
	public Guid Id { get; set; }
	public string EmployeeCode { get; set; }
	public string FullName { get; set; }
	public string? Designation { get; set; }
}
