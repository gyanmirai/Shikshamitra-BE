using System;

namespace ShikshaMitra.HR.Reports;

public class EmployeeContactDto
{
	public string? EmployeeName { get; set; }
	public string? Email { get; set; }
	public string? MobileNo { get; set; }
	public DateOnly? DateOfBirth { get; set; }
	public string? EmployeeCode { get; set; }
}