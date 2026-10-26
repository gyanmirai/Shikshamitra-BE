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
				: employee.ReportingManagerUser?.TenantId == employee.TenantId && employee.ReportingManagerUser != null
					? ToAdministrator(employee.ReportingManagerUser) : null
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

	public static EmployeeBasicDto ToAdministrator(Volo.Abp.Identity.IdentityUser user) => new()
	{
		Id = user.Id,
		Kind = "TenantAdministrator",
		FullName = string.Join(" ", new[] { user.Name, user.Surname }.Where(part => !string.IsNullOrWhiteSpace(part))) is { Length: > 0 } name
			? name : user.UserName,
		Email = user.Email,
		Designation = "Tenant administrator",
		Status = user.IsActive ? 0 : 1
	};

	public static EmployeeBasicDto ToEmployeeBasic(Employee employee)
	{
		return new EmployeeBasicDto
		{
			Id = employee.Id,
			Email = employee.Email,
			Status = (int)(employee.EmployeeStatusType ?? Common.EmployeeStatusType.Active),
			EmployeeCode = employee.EmployeeCode,
			FullName = employee.EmployeeName,
			Designation = employee.Designation
		};
	}
}
