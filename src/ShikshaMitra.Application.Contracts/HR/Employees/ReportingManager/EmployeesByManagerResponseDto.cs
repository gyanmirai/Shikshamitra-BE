using System.Collections.Generic;

namespace ShikshaMitra.HR.Employees.ReportingManager;

public class EmployeesByManagerResponseDto
{
	public EmployeeBasicDto Manager { get; set; } = new();
	public List<EmployeeBasicDto> Employees { get; set; } = new();
}
