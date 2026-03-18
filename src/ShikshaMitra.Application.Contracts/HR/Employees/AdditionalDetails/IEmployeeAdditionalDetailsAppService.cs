using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Employees.AdditionalDetails;

/// <summary>
/// Application service for managing employee additional details.
/// </summary>
public interface IEmployeeAdditionalDetailsAppService : IApplicationService
{
	/// <summary>
	/// Gets the additional details for an employee.
	/// </summary>
	/// <param name="employeeId">The employee ID.</param>
	/// <returns>The employee additional details.</returns>
	/// <exception cref="Volo.Abp.Domain.Entities.EntityNotFoundException">
	/// Thrown when the employee or additional details are not found.
	/// </exception>
	Task<EmployeeAdditionalDetailsDto> GetAsync(Guid employeeId);

	/// <summary>
	/// Creates or updates the additional details for an employee (upsert).
	/// </summary>
	/// <param name="employeeId">The employee ID.</param>
	/// <param name="input">The additional details to upsert.</param>
	/// <returns>
	/// A tuple containing the upserted additional details and a boolean indicating
	/// whether a new record was created (true) or an existing one was updated (false).
	/// </returns>
	/// <exception cref="Volo.Abp.Domain.Entities.EntityNotFoundException">
	/// Thrown when the employee is not found.
	/// </exception>
	Task<(EmployeeAdditionalDetailsDto Result, bool IsCreated)> UpsertAsync(
		Guid employeeId,
		EmployeeAdditionalDetailsUpsertDto input);

	/// <summary>
	/// Partially updates the additional details for an employee.
	/// </summary>
	/// <param name="employeeId">The employee ID.</param>
	/// <param name="input">The fields to update.</param>
	/// <returns>The updated additional details.</returns>
	/// <exception cref="Volo.Abp.Domain.Entities.EntityNotFoundException">
	/// Thrown when the employee or additional details are not found.
	/// </exception>
	Task<EmployeeAdditionalDetailsDto> PatchAsync(
		Guid employeeId,
		EmployeeAdditionalDetailsPatchDto input);
}



