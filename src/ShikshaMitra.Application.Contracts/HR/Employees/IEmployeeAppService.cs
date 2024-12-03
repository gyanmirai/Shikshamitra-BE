using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Employees;

public interface
	IEmployeeAppService : ICrudAppService<EmployeeDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateEmployeeDto>
{
	/// <summary>
	/// Retrieves an employee by the associated user ID.
	/// </summary>
	/// <param name="userId">The ID of the user.</param>
	/// <returns>The Employee DTO if found; otherwise, null.</returns>
	Task<EmployeeDto?> GetEmployeeByUserId(Guid userId);

	/// <summary>
	/// Creates a new employee with basic information.
	/// </summary>
	/// <param name="input">The DTO containing basic employee information.</param>
	/// <returns>The created Employee DTO.</returns>
	Task<EmployeeDto> CreateBasicInfoAsync(CreateEmployeeBasicInfoDto input);

	/// <summary>
	/// Updates the family information of an existing employee.
	/// </summary>
	/// <param name="employeeId">The ID of the employee to update.</param>
	/// <param name="input">The DTO containing updated family details.</param>
	/// <returns>The updated Employee DTO.</returns>
	Task<EmployeeDto> UpdateFamilyInfo(Guid employeeId, UpdateEmployeeFamilyDetailsDto input);

	/// <summary>
	/// Updates the work experience information of an existing employee.
	/// </summary>
	/// <param name="employeeId">The ID of the employee to update.</param>
	/// <param name="input">A list of DTOs containing work experience details.</param>
	/// <returns>The updated Employee DTO.</returns>
	Task<EmployeeDto> UpdateExperienceInfo(Guid employeeId, List<CreateUpdateWorkExperienceDto> input);

	/// <summary>
	/// Updates the bank details of an existing employee.
	/// </summary>
	/// <param name="employeeId">The ID of the employee to update.</param>
	/// <param name="input">The DTO containing bank details.</param>
	/// <returns>The updated Employee DTO.</returns>
	Task<EmployeeDto> UpdateBankDetails(Guid employeeId, BankDetailsDto input);

	Task UploadDocumentsAsync(Guid employeeId, List<DocumentDto> documentDtos);
	Task<DocumentDto> GetDocumentAsync(Guid employeeId, Guid documentId);
	Task<List<DocumentDto>> GetDocumentsAsync(Guid employeeId);
}