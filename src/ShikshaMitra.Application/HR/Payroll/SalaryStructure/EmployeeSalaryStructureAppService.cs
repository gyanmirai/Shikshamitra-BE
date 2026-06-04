using System;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.Settings;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.HR.Payroll.SalaryStructure;

/// <summary>
/// Application service for managing employee salary structure.
/// </summary>
[RemoteService(false)]
public class EmployeeSalaryStructureAppService(
	IEmployeeRepository employeeRepository,
	EmployeeSalaryStructureReadService readService,
	EmployeeSalaryStructureUpdateWorkflow updateWorkflow)
	: ApplicationService, IEmployeeSalaryStructureAppService
{
	/// <inheritdoc />
	public async Task<SalaryStructureResponseDto> GetAsync(Guid employeeId)
	{
		await EnsureEmployeeExistsAsync(employeeId);
		return await readService.GetAsync(employeeId);
	}

	/// <inheritdoc />
	public async Task UpdateAsync(Guid employeeId, UpdateSalaryStructureDto input)
	{
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
