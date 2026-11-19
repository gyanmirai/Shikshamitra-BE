using System;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.Settings;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using H = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.HR;

namespace ShikshaMitra.HR.Payroll.SalaryStructure;

/// <summary>
/// Application service for managing employee salary structure.
/// </summary>
[RemoteService(false)]
public class EmployeeSalaryStructureAppService(
	IEmployeeRepository employeeRepository,
	EmployeeSalaryStructureReadService readService,
	EmployeeSalaryStructureUpdateWorkflow updateWorkflow,
	IEmployeeAuthorizationService access)
	: ApplicationService, IEmployeeSalaryStructureAppService
{
	/// <inheritdoc />
	public async Task<SalaryStructureResponseDto> GetAsync(Guid employeeId)
	{
		await AuthorizationService.CheckAsync(H.Employee_EmployeeList.View);
		await access.CheckEmployeeAccessAsync(employeeId, requireOwnership: true);
		await EnsureEmployeeExistsAsync(employeeId);
		return await readService.GetAsync(employeeId);
	}

	/// <inheritdoc />
	public async Task UpdateAsync(Guid employeeId, UpdateSalaryStructureDto input)
	{
		await AuthorizationService.CheckAsync(H.PayrollManagement_PayrollStructure.Add);
		await access.CheckEmployeeModifyAccessAsync(employeeId);
		await EnsureEmployeeExistsAsync(employeeId);
		await updateWorkflow.UpdateAsync(employeeId, input);
	}

	private async Task EnsureEmployeeExistsAsync(Guid employeeId)
	{
		var employee = await employeeRepository.FindAsync(employeeId, includeDetails: false);
		if (employee == null)
		{
			throw new EntityNotFoundException(typeof(Employee), employeeId);
		}
	}
}
