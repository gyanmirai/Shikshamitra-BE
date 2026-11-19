using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.HR.Employees;

public class EmployeeReportingManager(
	IEmployeeRepository employeeRepository,
	TenantAdministratorReportingManagerReader administratorReader) : DomainService
{
	public async Task<Employee> AssignAdministratorAsync(Guid employeeId, Guid userId)
	{
		var employee = await employeeRepository.GetAsync(employeeId);
		var administrator = (await administratorReader.GetEligibleAsync()).Find(user => user.Id == userId);
		if (administrator == null || employee.TenantId != CurrentTenant.Id)
			throw new BusinessException(HRErrorCodes.ReportingManagerMustBeActive);
		if (employee.UserId == userId)
			throw new BusinessException(ShikshaMitraDomainErrorCodes.EmployeeCannotReportToSelf);
		employee.AssignAdministratorReportingManager(userId);
		return await employeeRepository.UpdateAsync(employee);
	}

	public async Task<Employee> AssignReportingManagerAsync(Guid employeeId, Guid reportingManagerId)
	{
		var employee = await employeeRepository.GetAsync(employeeId);
		var reportingManager = await employeeRepository.GetAsync(reportingManagerId);

		if ((reportingManager.EmployeeStatusType ?? EmployeeStatusType.Active) != EmployeeStatusType.Active)
		{
			throw new BusinessException(HRErrorCodes.ReportingManagerMustBeActive)
				.WithData("EmployeeId", employeeId)
				.WithData("ReportingManagerId", reportingManagerId)
				.WithData("ReportingManagerStatus", (object?)reportingManager.EmployeeStatusType ?? null!);
		}

		if (employeeId == reportingManagerId)
		{
			throw new BusinessException(ShikshaMitraDomainErrorCodes.EmployeeCannotReportToSelf)
				.WithData("EmployeeId", employeeId);
		}

		await EnsureNoCircularReportingAsync(employeeId, reportingManagerId);

		employee.AssignReportingManager(reportingManagerId);
		return await employeeRepository.UpdateAsync(employee);
	}

	private async Task EnsureNoCircularReportingAsync(Guid employeeId, Guid newManagerId)
	{
		var currentManagerId = newManagerId;
		var visited = new HashSet<Guid> { employeeId };

		while (currentManagerId != Guid.Empty)
		{
			if (!visited.Add(currentManagerId))
			{
				throw new BusinessException(ShikshaMitraDomainErrorCodes.CircularReportingNotAllowed)
					.WithData("EmployeeId", employeeId)
					.WithData("ManagerId", newManagerId);
			}

			var manager = await employeeRepository.GetWithReportingManagerAsync(currentManagerId);
			currentManagerId = manager?.ReportingManagerId ?? Guid.Empty;
		}
	}
}
