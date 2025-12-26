using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Employees.FamilyInfo;

/// <summary>
/// Application service for managing employee family information.
/// </summary>
public interface IEmployeeFamilyInfoAppService : IApplicationService
{
	/// <summary>
	/// Gets the family information for an employee.
	/// </summary>
	/// <param name="employeeId">The employee ID.</param>
	/// <returns>The employee family information.</returns>
	/// <exception cref="Volo.Abp.Domain.Entities.EntityNotFoundException">
	/// Thrown when the employee is not found.
	/// </exception>
	Task<FamilyInfoResponse> GetAsync(Guid employeeId);

	/// <summary>
	/// Creates or updates the family information for an employee (upsert).
	/// </summary>
	/// <param name="employeeId">The employee ID.</param>
	/// <param name="input">The family information to upsert.</param>
	/// <exception cref="Volo.Abp.Domain.Entities.EntityNotFoundException">
	/// Thrown when the employee is not found.
	/// </exception>
	Task UpsertAsync(Guid employeeId, FamilyInfoRequest input);
}

