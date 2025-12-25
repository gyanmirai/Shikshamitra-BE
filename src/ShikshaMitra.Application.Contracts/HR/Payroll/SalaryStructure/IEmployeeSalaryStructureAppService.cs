using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Payroll.SalaryStructure;

/// <summary>
/// Application service interface for managing employee salary structure.
/// </summary>
public interface IEmployeeSalaryStructureAppService : IApplicationService
{
	/// <summary>
	/// Gets the salary structure for an employee.
	/// </summary>
	/// <param name="employeeId">The ID of the employee.</param>
	/// <returns>The employee's salary structure.</returns>
	Task<SalaryStructureResponseDto> GetAsync(Guid employeeId);

	/// <summary>
	/// Updates the salary structure for an employee.
	/// </summary>
	/// <param name="employeeId">The ID of the employee.</param>
	/// <param name="input">The salary structure to set.</param>
	/// <returns>A task representing the async operation.</returns>
	Task UpdateAsync(Guid employeeId, UpdateSalaryStructureDto input);
}

