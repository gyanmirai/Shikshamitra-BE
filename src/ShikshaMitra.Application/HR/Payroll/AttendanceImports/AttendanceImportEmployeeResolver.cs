using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.Payroll.AttendanceImports;

public class AttendanceImportEmployeeResolver(
	IRepository<Employee, Guid> employeeRepository,
	IAsyncQueryableExecuter asyncExecuter)
	: ITransientDependency
{
	public async Task<Dictionary<string, Guid>> BuildLookupByCodeAsync(List<AttendanceImportRow> rows)
	{
		var employeeCodes = rows
			.Select(row => row.EmployeeCode?.Trim())
			.Where(code => !string.IsNullOrWhiteSpace(code))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();
		if (employeeCodes.Count == 0)
		{
			return new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
		}

		var normalizedEmployeeCodes = employeeCodes
			.Select(code => code!.ToUpperInvariant())
			.ToList();

		var employeeQuery = await employeeRepository.GetQueryableAsync();

		var employees = await asyncExecuter.ToListAsync(
			employeeQuery
				.Where(employee =>
					employee.EmployeeCode != null &&
					normalizedEmployeeCodes.Contains(employee.EmployeeCode.ToUpper()))
				.Select(employee => new { employee.EmployeeCode, employee.Id }));
		return employees
			.GroupBy(x => x.EmployeeCode, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(group => group.Key, group => group.First().Id, StringComparer.OrdinalIgnoreCase);
	}

	public async Task<Dictionary<Guid, Employee>> BuildLookupByIdAsync(List<Guid> employeeIds)
	{
		if (employeeIds.Count == 0)
		{
			return [];
		}

		var employeeQuery = await employeeRepository.GetQueryableAsync();

		var employees = await asyncExecuter.ToListAsync(employeeQuery.Where(x => employeeIds.Contains(x.Id)));
		return employees.ToDictionary(x => x.Id);
	}
}
