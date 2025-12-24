using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Employees.WorkExperienceContracts;

/// <summary>
/// Application service for managing employee work experience.
/// </summary>
public interface IEmployeeWorkExperienceAppService : IApplicationService
{
	/// <summary>
	/// Gets the work experience list for an employee.
	/// </summary>
	/// <param name="employeeId">The employee ID.</param>
	/// <returns>The list of work experience records.</returns>
	/// <exception cref="Volo.Abp.Domain.Entities.EntityNotFoundException">
	/// Thrown when the employee is not found.
	/// </exception>
	Task<List<WorkExperienceContract>> GetAsync(Guid employeeId);

	/// <summary>
	/// Updates the work experience list for an employee (replaces all records).
	/// </summary>
	/// <param name="employeeId">The employee ID.</param>
	/// <param name="input">The work experience records to set.</param>
	/// <exception cref="Volo.Abp.Domain.Entities.EntityNotFoundException">
	/// Thrown when the employee is not found.
	/// </exception>
	Task UpsertAsync(Guid employeeId, List<WorkExperienceContract> input);
}

