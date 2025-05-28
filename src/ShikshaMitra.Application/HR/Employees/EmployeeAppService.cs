using ShikshaMitra.Common;
using ShikshaMitra.HR.Payroll.PayrollStructures;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.HR.Employees;

public class EmployeeAppService(
	IEmployeeRepository employeeRepository,
	IPayrollStructureRepository payrollStructureRepository,
	EmployeeManager employeeManager,
	PayrollStructureManager payrollStructureManager,
	IBlobContainer blobContainer) :
	CrudAppService<Employee, EmployeeDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateEmployeeDto>(
		employeeRepository),
	IEmployeeAppService
{
	public async Task<EmployeeDto?> GetEmployeeByUserId(Guid userId)
	{
		Employee employee = await employeeRepository.GetEmployeeByUserIdAsync(userId) ?? throw new EntityNotFoundException(typeof(Employee), userId);

		return ObjectMapper.Map<Employee, EmployeeDto>(employee);
	}

	public async Task<EmployeeDto> CreateBasicInfoAsync(CreateEmployeeBasicInfoDto input)
	{
		// Use EmployeeManager to create the employee with domain logic
		Employee employee = await employeeManager.CreateEmployeeAsync(
			roleId: input.RoleId,
			educationMediumStageId: input.EducationMediumStageId,
			firstName: input.FirstName,
			lastName: input.LastName,
			middleName: input.MiddleName,
			shortName: input.ShortName,
			employeeStatusType: input.EmployeeStatusType,
			dateOfBirth: input.DateOfBirth,
			profilePicture: input.ProfilePicture,
			familyCast: input.FamilyCast,
			castType: input.CastType,
			genderType: input.GenderType,
			religion: input.Religion,
			maritalStatus: input.MaritalStatus,
			bloodGroup: input.BloodGroup,
			mobileNo: input.MobileNo,
			email: input.Email,
			address1: input.Address1,
			address2: input.Address2,
			address3: input.Address3,
			department: input.Department,
			designation: input.Designation,
			dateOfJoining: input.DateOfJoining,
			employeeGroup: input.EmployeeGroup,
			qualification: input.Qualification,
			identificationMark1: input.IdentificationMark1,
			city: input.City,
			state: input.State,
			country: input.Country,
			pinCode: input.PinCode,
			shiftId: input.ShiftId
		);

		// Save the employee
		employee = await employeeRepository.InsertAsync(employee);

		// Return the DTO
		return ObjectMapper.Map<Employee, EmployeeDto>(employee);
	}

	public async Task<EmployeeDto> UpdateBasicInfoAsync(Guid employeeId, UpdateEmployeeBasicInfoDto input)
	{
		// Update employee using EmployeeManager
		Employee employee = await employeeManager.UpdateEmployeeAsync(
			employeeId,
			firstName: input.FirstName,
			lastName: input.LastName,
			middleName: input.MiddleName,
			shortName: input.ShortName,
			employeeStatusType: input.EmployeeStatusType,
			dateOfBirth: input.DateOfBirth,
			profilePicture: input.ProfilePicture,
			familyCast: input.FamilyCast,
			castType: input.CastType,
			genderType: input.GenderType,
			religion: input.Religion,
			maritalStatus: input.MaritalStatus,
			bloodGroup: input.BloodGroup,
			mobileNo: input.MobileNo,
			email: input.Email,
			address1: input.Address1,
			address2: input.Address2,
			address3: input.Address3,
			department: input.Department,
			designation: input.Designation,
			dateOfJoining: input.DateOfJoining,
			employeeGroup: input.EmployeeGroup,
			qualification: input.Qualification,
			roleId: input.RoleId,
			educationMediumStageId: input.EducationMediumStageId,
			identificationMark1: input.IdentificationMark1,
			city: input.City,
			state: input.State,
			country: input.Country,
			pinCode: input.PinCode,
			shiftId: input.ShiftId
		);

		// Save the updated employee
		employee = await employeeRepository.UpdateAsync(employee);

		// Return the updated DTO
		return ObjectMapper.Map<Employee, EmployeeDto>(employee);
	}

	public async Task<EmployeeDto> UpdateFamilyInfo(Guid employeeId, UpdateEmployeeFamilyDetailsDto input)
	{
		// convert the input to domain model and update the employee

		ContactDetails? fatherDetails = ObjectMapper.Map<ContactDetailsDto, ContactDetails>(input.FatherDetails);
		ContactDetails? motherDetails = ObjectMapper.Map<ContactDetailsDto, ContactDetails>(input.MotherDetails);
		ContactDetails? spouseDetails = ObjectMapper.Map<ContactDetailsDto, ContactDetails>(input.SpouseDetails);

		Employee? employee =
			await employeeManager.UpdateFamilyInfoByEmployeeIdAsync(employeeId, fatherDetails, motherDetails,
				spouseDetails);

		// Save the employee
		employee = await employeeRepository.UpdateAsync(employee);

		// Return the DTO
		return ObjectMapper.Map<Employee, EmployeeDto>(employee);
	}

	public async Task UploadDocumentsAsync(Guid employeeId, List<DocumentDto> documents)
	{
		// Map the DTOs to domain entities or value objects
		List<EmployeeDocumentData> employeeDocuments = documents.ConvertAll(doc => new EmployeeDocumentData
		{
			FileName = doc.FileName,
			ContentType = doc.ContentType,
			Content = doc.Content,
			Size = doc.Size
		});

		// Call the domain service
		await employeeManager.AddDocumentsAsync(employeeId, employeeDocuments);
	}

	public async Task<DocumentDto> GetDocumentAsync(Guid employeeId, Guid documentId)
	{
		EmployeeDocument document = await employeeManager.GetDocumentAsync(employeeId, documentId);

		await using var stream = await blobContainer.GetAsync(document.BlobName);
		await using var memoryStream = new MemoryStream();
		await stream.CopyToAsync(memoryStream);
		return new DocumentDto
		{
			FileName = document.DocumentName,
			ContentType = document.ContentType,
			Content = memoryStream.ToArray(),
			Size = document.Size
		};
	}

	public async Task<List<DocumentMetadataDto>> GetDocumentMetadataAsync(Guid employeeId)
	{
		var documents = await employeeManager.GetDocumentsAsync(employeeId);
		return documents.Select(doc => new DocumentMetadataDto
		{
			Id = doc.Id,
			FileName = doc.DocumentName,
			Size = doc.Size,
			ContentType = doc.ContentType
		}).ToList();
	}

	public async Task<DocumentDto> UpdateDocumentAsync(Guid employeeId, Guid documentId, DocumentDto documentDto)
	{
		var updatedDocument = await employeeManager.UpdateDocumentAsync(
			employeeId,
			documentId,
			documentDto.FileName,
			documentDto.ContentType,
			documentDto.Size,
			documentDto.Content
		);

		return new DocumentDto
		{
			FileName = updatedDocument.DocumentName,
			ContentType = updatedDocument.ContentType,
			Content = documentDto.Content,
			Size = updatedDocument.Size,
			Id = updatedDocument.Id
		};
	}


	public async Task<EmployeeDto> UpdateExperienceInfo(Guid employeeId, List<CreateUpdateWorkExperienceDto> input)
	{
		// convert the input to domain model and update the employee

		List<WorkExperience> workExperiences =
			ObjectMapper.Map<List<CreateUpdateWorkExperienceDto>, List<WorkExperience>>(input);

		Employee employee =
			await employeeManager.UpdateExperienceInfoByEmployeeIdAsync(employeeId, workExperiences);

		// Save the employee
		employee = await employeeRepository.UpdateAsync(employee);

		// Return the DTO
		return ObjectMapper.Map<Employee, EmployeeDto>(employee);
	}

	public async Task<EmployeeDto> UpdateBankDetails(Guid employeeId, BankDetailsDto input)
	{
		BankDetails bankDetails = ObjectMapper.Map<BankDetailsDto, BankDetails>(input);

		Employee employee =
			await employeeManager.UpdateBankDetailsByEmployeeIdAsync(employeeId, bankDetails);

		// Save the employee
		employee = await employeeRepository.UpdateAsync(employee);

		// Return the DTO
		return ObjectMapper.Map<Employee, EmployeeDto>(employee);
	}

	public async Task<EmployeeDto> UpdateEmployeeStatus(Guid employeeId, EmployeeStatusType employeeStatusType,
		string? changeReason)
	{
		Employee employee =
			await employeeManager.UpdateEmployeeStatusAsync(employeeId, employeeStatusType, changeReason);

		// Save the employee
		employee = await employeeRepository.UpdateAsync(employee);

		// Return the DTO
		return ObjectMapper.Map<Employee, EmployeeDto>(employee);
	}


	[RemoteService(false)]

	public async Task SetEmployeePayrollStructure(Guid employeeId, Guid payrollStructureId)
	{
		await payrollStructureManager.SetEmployeePayrollStructureAsync(employeeId, payrollStructureId);
	}

	[RemoteService(false)]
	public async Task RemoveEmployeePayrollStructure(Guid employeeId)
	{
		await payrollStructureManager.RemoveEmployeePayrollStructureAsync(employeeId);
	}

	public async Task UpdatePayrollStructureAsync(Guid employeeId, CreateUpdateEmployeePayrollStructureDto input)
	{

	}
}