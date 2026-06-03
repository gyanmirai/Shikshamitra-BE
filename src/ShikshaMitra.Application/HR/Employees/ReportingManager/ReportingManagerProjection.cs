using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.HR.Employees.ReportingManager;

public static class ReportingManagerProjection
{
	public static EmployeeReportingResponseDto ToReportingResponse(Employee employee)
	{
		return new EmployeeReportingResponseDto
		{
			EmployeeId = employee.Id,
			ReportingManager = employee.ReportingManager != null
				? ToEmployeeBasic(employee.ReportingManager)
				: null
		};
	}

	public static EmployeesByManagerResponseDto ToEmployeesByManagerResponse(Employee manager, IEnumerable<Employee> employees)
	{
		return new EmployeesByManagerResponseDto
		{
			Manager = ToEmployeeBasic(manager),
			Employees = employees.Select(ToEmployeeBasic).ToList()
		};
	}

	private static EmployeeBasicDto ToEmployeeBasic(Employee employee)
	{
		return new EmployeeBasicDto
		{
			Id = employee.Id,
			EmployeeCode = employee.EmployeeCode,
			FullName = employee.EmployeeName,
			Designation = employee.Designation
		};
	}
}
