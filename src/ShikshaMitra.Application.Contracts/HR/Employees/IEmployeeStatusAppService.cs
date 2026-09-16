using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Employees;

/// <summary>
/// Application service interface for managing employee status.
/// </summary>
public interface IEmployeeStatusAppService : IApplicationService
{
	/// <summary>
	/// Changes the status of an employee.
	/// </summary>
	/// <param name="employeeId">The ID of the employee.</param>
	/// <param name="input">The status change request.</param>
	/// <returns>Response containing previous and current status.</returns>
	Task<EmployeeStatusResponseDto> ChangeStatusAsync(Guid employeeId, ChangeEmployeeStatusDto input);

	/// <summary>
	/// Checks whether an employee can be moved to a non-active status.
	/// </summary>
	Task<EmployeeStatusDeactivationCheckDto> GetDeactivationCheckAsync(Guid employeeId);

	Task<EmployeeActivationReadinessDto> GetActivationReadinessAsync(Guid employeeId);
}

