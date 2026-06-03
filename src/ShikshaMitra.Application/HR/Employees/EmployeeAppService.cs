using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees.Documents;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Employees.PersonalInfo;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Employees;

[Obsolete("Use focused employee services instead. See docs/implementation/hr/application-layer-patterns.md")]
[RemoteService(false)]
public class EmployeeAppService(
	IEmployeeRepository employeeRepository,
	IEmployeeDocumentAppService employeeDocumentAppService,
	IEmployeePayrollAppService employeePayrollAppService,
	IEmployeePersonalInfoAppService employeePersonalInfoAppService,
	EmployeeBasicInfoWorkflow basicInfoWorkflow,
	EmployeeLegacyFacadeWorkflow legacyWorkflow)
	: CrudAppService<Employee, EmployeeDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateEmployeeDto>(
		employeeRepository),
		IEmployeeAppService
{
	public Task<EmployeeDto> GetEmployeeByUserId(Guid userId) =>
		employeePersonalInfoAppService.GetByUserIdAsync(userId);

	public override Task<EmployeeDto> GetAsync(Guid id) =>
		employeePersonalInfoAppService.GetWithDetailsAsync(id);

	public Task<EmployeeDto> CreateBasicInfoAsync(CreateEmployeeBasicInfoDto input) =>
		basicInfoWorkflow.CreateAsync(input);

	public Task<EmployeeDto> UpdateBasicInfoAsync(Guid employeeId, UpdateEmployeeBasicInfoDto input) =>
		basicInfoWorkflow.UpdateAsync(employeeId, input);

	public Task<EmployeeDto> UpdateFamilyInfo(Guid employeeId, UpdateEmployeeFamilyDetailsDto input) =>
		legacyWorkflow.UpdateFamilyInfo(employeeId, input);

	public Task UploadDocumentsAsync(Guid employeeId, List<DocumentDto> documents) =>
		employeeDocumentAppService.UploadDocumentsAsync(employeeId, documents);

	public Task<DocumentDto> GetDocumentAsync(Guid employeeId, Guid documentId) =>
		employeeDocumentAppService.GetDocumentAsync(employeeId, documentId);

	public Task<List<DocumentMetadataDto>> GetDocumentMetadataAsync(Guid employeeId) =>
		employeeDocumentAppService.GetDocumentMetadataAsync(employeeId);

	public Task<DocumentDto> UpdateDocumentAsync(Guid employeeId, Guid documentId, DocumentDto documentDto) =>
		employeeDocumentAppService.UpdateDocumentAsync(employeeId, documentId, documentDto);

	public Task DeleteDocumentAsync(Guid employeeId, Guid documentId) =>
		employeeDocumentAppService.DeleteDocumentAsync(employeeId, documentId);

	public Task<string> UploadProfilePictureAsync(Guid employeeId, string fileName, byte[] fileContent) =>
		employeeDocumentAppService.UploadProfilePictureAsync(employeeId, fileName, fileContent);

	public Task<DocumentDto?> GetProfilePictureAsync(Guid employeeId) =>
		employeeDocumentAppService.GetProfilePictureAsync(employeeId);

	public Task<EmployeeDto> UpdateExperienceInfo(Guid employeeId, List<CreateUpdateWorkExperienceDto> input) =>
		legacyWorkflow.UpdateExperienceInfo(employeeId, input);

	public Task<EmployeeDto> UpdateBankDetails(Guid employeeId, BankDetailsDto input) =>
		employeePersonalInfoAppService.UpdateBankDetailsAsync(employeeId, input);

	public Task<EmployeeAdditionalDetailsDto> GetAdditionalDetailsAsync(Guid employeeId) =>
		legacyWorkflow.GetAdditionalDetailsAsync(employeeId);

	public Task<EmployeeAdditionalDetailsDto> UpdateAdditionalDetailsAsync(
		Guid employeeId,
		UpdateEmployeeAdditionalDetailsDto input) =>
		legacyWorkflow.UpdateAdditionalDetailsAsync(employeeId, input);

	public Task<EmployeeDto> UpdateEmployeeStatus(
		Guid employeeId,
		EmployeeStatusType employeeStatusType,
		string? changeReason,
		DateOnly? effectiveDate = null) =>
		legacyWorkflow.UpdateEmployeeStatus(employeeId, employeeStatusType, changeReason, effectiveDate);

	public Task<EmployeePayrollDetailsDto> GetPayrollDetailsAsync(Guid employeeId) =>
		employeePayrollAppService.GetPayrollDetailsAsync(employeeId);

	public Task UpdatePayrollDetailsAsync(Guid employeeId, UpdateEmployeePayrollDetailsDto input) =>
		employeePayrollAppService.UpdatePayrollDetailsAsync(employeeId, input);

	public Task<EmployeeListResponse> GetFilteredListAsync(GetEmployeeListInput input) =>
		legacyWorkflow.GetFilteredListAsync(input);

	public Task<EmployeePayrollResponse> GetPayrollContractAsync(Guid employeeId) =>
		employeePayrollAppService.GetPayrollContractAsync(employeeId);

	public Task UpdatePayrollContractAsync(Guid employeeId, PayrollContract input) =>
		employeePayrollAppService.UpdatePayrollContractAsync(employeeId, input);
}
