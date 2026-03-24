using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees.Payroll;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Employees;

/// <summary>
/// Deprecated: This interface has been decomposed into focused services.
/// Use IEmployeePersonalInfoAppService, IEmployeeDocumentAppService,
/// IEmployeePayrollAppService, IEmployeeSearchAppService,
/// IEmployeeFamilyInfoAppService, IEmployeeWorkExperienceAppService,
/// IEmployeeAdditionalDetailsAppService, and IEmployeeStatusAppService instead.
/// </summary>
[Obsolete("Use the focused employee service interfaces instead. See docs/implementation/hr/application-layer-patterns.md")]
public interface
	IEmployeeAppService : ICrudAppService<EmployeeDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateEmployeeDto>
{
	/// <summary>
	/// Retrieves an employee by the associated user ID.
	/// </summary>
	/// <param name="userId">The ID of the user.</param>
	/// <returns>The Employee DTO.</returns>
	/// <exception cref="Volo.Abp.Domain.Entities.EntityNotFoundException">Thrown when employee is not found.</exception>
	Task<EmployeeDto> GetEmployeeByUserId(Guid userId);

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

	/// <summary>
	/// Gets the additional details for an employee.
	/// </summary>
	/// <param name="employeeId">The ID of the employee.</param>
	/// <returns>Employee additional details DTO.</returns>
	Task<EmployeeAdditionalDetailsDto> GetAdditionalDetailsAsync(Guid employeeId);

	/// <summary>
	/// Updates the additional details for an employee.
	/// </summary>
	/// <param name="employeeId">The ID of the employee.</param>
	/// <param name="input">The DTO containing updated additional details.</param>
	/// <returns>Updated additional details DTO.</returns>
	Task<EmployeeAdditionalDetailsDto> UpdateAdditionalDetailsAsync(Guid employeeId, UpdateEmployeeAdditionalDetailsDto input);

	Task UploadDocumentsAsync(Guid employeeId, List<DocumentDto> documentDtos);
	Task<DocumentDto> GetDocumentAsync(Guid employeeId, Guid documentId);
	Task<List<DocumentMetadataDto>> GetDocumentMetadataAsync(Guid employeeId);
	Task<DocumentDto> UpdateDocumentAsync(Guid employeeId, Guid documentId, DocumentDto documentDto);
	Task DeleteDocumentAsync(Guid employeeId, Guid documentId);

	/// <summary>
	/// Uploads a profile picture for an employee.
	/// </summary>
	/// <param name="employeeId">The ID of the employee.</param>
	/// <param name="fileName">The name of the file.</param>
	/// <param name="fileContent">The file content as byte array.</param>
	/// <returns>The blob name of the uploaded profile picture.</returns>
	Task<string> UploadProfilePictureAsync(Guid employeeId, string fileName, byte[] fileContent);

	/// <summary>
	/// Gets the profile picture for an employee.
	/// </summary>
	/// <param name="employeeId">The ID of the employee.</param>
	/// <returns>The profile picture as a DocumentDto.</returns>
	Task<DocumentDto?> GetProfilePictureAsync(Guid employeeId);

	/// <summary>
	/// Updates the basic information of an existing employee.
	/// </summary>
	/// <param name="employeeId">The ID of the employee to update.</param>
	/// <param name="input">The DTO containing updated basic employee information.</param>
	/// <returns>The updated Employee DTO.</returns>
	Task<EmployeeDto> UpdateBasicInfoAsync(Guid employeeId, UpdateEmployeeBasicInfoDto input);

	/// <summary>
	/// Updates the status of an existing employee.
	/// </summary>
	/// <param name="employeeId">The ID of the employee to update.</param>
	/// <param name="employeeStatusType">The new status type.</param>
	/// <param name="changeReason">The reason for the status change.</param>
	/// <returns>The updated Employee DTO.</returns>
	Task<EmployeeDto> UpdateEmployeeStatus(Guid employeeId, EmployeeStatusType employeeStatusType, string? changeReason);

	Task UpdatePayrollStructureAsync(Guid employeeId, CreateUpdateEmployeePayrollStructureDto input);

	/// <summary>
	/// Gets the complete payroll details for an employee including earnings and deductions.
	/// </summary>
	/// <param name="employeeId">The ID of the employee.</param>
	/// <returns>Complete payroll details including bank info, earnings, and deductions.</returns>
	Task<EmployeePayrollDetailsDto> GetPayrollDetailsAsync(Guid employeeId);

	/// <summary>
	/// Updates the complete payroll details for an employee.
	/// </summary>
	/// <param name="employeeId">The ID of the employee.</param>
	/// <param name="input">The payroll details to update.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	Task UpdatePayrollDetailsAsync(Guid employeeId, UpdateEmployeePayrollDetailsDto input);

	/// <summary>
	/// Gets the payroll details for an employee (matches OpenAPI Payroll contract).
	/// </summary>
	/// <param name="employeeId">The ID of the employee.</param>
	/// <returns>Payroll contract response.</returns>
	Task<PayrollContract> GetPayrollContractAsync(Guid employeeId);

	/// <summary>
	/// Updates the payroll details for an employee (matches OpenAPI Payroll contract).
	/// </summary>
	/// <param name="employeeId">The ID of the employee.</param>
	/// <param name="input">The payroll contract to update.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	Task UpdatePayrollContractAsync(Guid employeeId, PayrollContract input);

	/// <summary>
	/// Gets a filtered and paginated list of employees.
	/// </summary>
	/// <param name="input">Filter and pagination parameters.</param>
	/// <returns>Paginated list of employee summaries.</returns>
	Task<EmployeeListResponse> GetFilteredListAsync(GetEmployeeListInput input);

	/// <summary>
	/// Searches employees by name, employee identifier, email, or mobile number.
	/// </summary>
	/// <param name="input">Search parameters.</param>
	/// <returns>Search results with employee details.</returns>
	Task<EmployeeSearchResponse> SearchAsync(SearchEmployeeInput input);
}