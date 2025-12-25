using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Employees.PersonalInfo;

/// <summary>
/// Application service for managing employee personal information.
/// </summary>
public interface IEmployeePersonalInfoAppService : IApplicationService
{
	/// <summary>
	/// Creates a new employee with personal information.
	/// </summary>
	/// <param name="input">The employee personal information to create.</param>
	/// <returns>The created employee personal information.</returns>
	Task<EmployeePersonalInfoDto> CreateAsync(CreateEmployeePersonalInfoDto input);

	/// <summary>
	/// Gets an employee's personal information by ID.
	/// </summary>
	/// <param name="id">The employee ID.</param>
	/// <returns>The employee personal information.</returns>
	Task<EmployeePersonalInfoDto> GetAsync(Guid id);

	/// <summary>
	/// Updates an existing employee's personal information.
	/// </summary>
	/// <param name="id">The employee ID.</param>
	/// <param name="input">The updated employee personal information.</param>
	/// <returns>The updated employee personal information.</returns>
	Task<EmployeePersonalInfoDto> UpdateAsync(Guid id, UpdateEmployeePersonalInfoDto input);
}

