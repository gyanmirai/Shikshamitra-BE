using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Employees.PersonalInfo;

/// <summary>
/// Application service for managing employee personal information and lifecycle.
/// Single owner of Create / Get / Update basic info.
/// </summary>
public interface IEmployeePersonalInfoAppService : IApplicationService
{
	Task<EmployeePersonalInfoDto> CreateAsync(CreateEmployeePersonalInfoDto input);

	/// <summary>
	/// Creates an employee with a profile picture uploaded in the same operation.
	/// </summary>
	Task<EmployeePersonalInfoDto> CreateWithProfilePictureAsync(
		CreateEmployeePersonalInfoDto input,
		string profilePictureFileName,
		byte[] profilePictureContent);

	Task<EmployeePersonalInfoDto> GetAsync(Guid id);
	Task<EmployeePersonalInfoDto> UpdateAsync(Guid id, UpdateEmployeePersonalInfoDto input);

	/// <summary>
	/// Retrieves an employee by the associated Identity user ID.
	/// </summary>
	Task<EmployeeDto> GetByUserIdAsync(Guid userId);

	/// <summary>
	/// Gets an employee with full details including PayrollStructure and family members.
	/// </summary>
	Task<EmployeeDto> GetWithDetailsAsync(Guid id);

	/// <summary>
	/// Updates the bank details for an employee.
	/// </summary>
	Task<EmployeeDto> UpdateBankDetailsAsync(Guid employeeId, BankDetailsDto input);
}

