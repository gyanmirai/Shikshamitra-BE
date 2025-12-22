using Microsoft.Extensions.Logging;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees.Payroll;
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
using Volo.Abp.Guids;

namespace ShikshaMitra.HR.Employees;

public class EmployeeAppService(
IEmployeeRepository employeeRepository,
IEmployeeAdditionalDetailsRepository additionalDetailsRepository,
EmployeeManager employeeManager,
PayrollStructureManager payrollStructureManager,
IBlobContainer blobContainer,
IEmployeeCacheManager employeeCacheManager,
IEmployeePayrollDetailsRepository payrollDetailsRepository,
IEmployeePayrollComponentRepository payrollComponentRepository,
IGuidGenerator guidGenerator,
ILogger<EmployeeAppService> logger) :
CrudAppService<Employee, EmployeeDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateEmployeeDto>(
employeeRepository),
IEmployeeAppService
{
	public async Task<EmployeeDto> GetEmployeeByUserId(Guid userId)
	{
		// Navigation properties (Role, EducationMediumStage, Shift) are auto-loaded
		Employee employee = await employeeRepository.GetEmployeeByUserIdAsync(userId) 
			?? throw new EntityNotFoundException(typeof(Employee), userId);

		return ObjectMapper.Map<Employee, EmployeeDto>(employee);
	}

	public override async Task<EmployeeDto> GetAsync(Guid id)
	{
		// Use GetWithDetailsAsync for complete employee data including PayrollStructure
		var employee = await employeeRepository.GetWithDetailsAsync(id, includePayrollStructure: true)
			?? throw new EntityNotFoundException(typeof(Employee), id);

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
			familyCast: input.FamilyCast,
			castType: input.CastType,
			genderType: input.GenderType,
			religion: input.Religion,
			maritalStatus: input.MaritalStatus,
			bloodGroup: input.BloodGroup,
			mobileNo: input.MobileNo,
			secondaryMobileNo: input.SecondaryMobileNo,
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
			familyCast: input.FamilyCast,
			castType: input.CastType,
			genderType: input.GenderType,
			religion: input.Religion,
			maritalStatus: input.MaritalStatus,
			bloodGroup: input.BloodGroup,
			mobileNo: input.MobileNo,
			secondaryMobileNo: input.SecondaryMobileNo,
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

		// Save the updated employee (cache will be invalidated in repository)
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

		// Save the employee (cache invalidation handled in repository)
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

	public async Task<EmployeeAdditionalDetailsDto> GetAdditionalDetailsAsync(Guid employeeId)
	{
		// Get employee to verify existence and get employee code
		var employee = await employeeRepository.GetAsync(employeeId)
			?? throw new EntityNotFoundException(typeof(Employee), employeeId);

		// Get additional details from the new entity-based repository
		var additionalDetails = await additionalDetailsRepository.GetByEmployeeIdAsync(employeeId);

		// Return defaults if no record exists yet
		if (additionalDetails == null)
		{
			return new EmployeeAdditionalDetailsDto
			{
				EmployeeCode = employee.EmployeeCode,
				BplCardNo = null,
				PersonWithDisability = false,
				SecondLanguage = null,
				LibraryMember = false,
				TransportMember = false
			};
		}

		// Map to DTO
		return new EmployeeAdditionalDetailsDto
		{
			EmployeeCode = employee.EmployeeCode,
			BplCardNo = additionalDetails.BplCardNo,
			PersonWithDisability = additionalDetails.PersonWithDisability,
			SecondLanguage = additionalDetails.SecondLanguage,
			LibraryMember = additionalDetails.LibraryMember,
			TransportMember = additionalDetails.TransportMember
		};
	}

	public async Task<EmployeeAdditionalDetailsDto> UpdateAdditionalDetailsAsync(
		Guid employeeId,
		UpdateEmployeeAdditionalDetailsDto input)
	{
		// Verify employee exists
		var employee = await employeeRepository.GetAsync(employeeId)
			?? throw new EntityNotFoundException(typeof(Employee), employeeId);

		// Check if additional details already exist
		var existingDetails = await additionalDetailsRepository.GetByEmployeeIdAsync(employeeId);

		EmployeeAdditionalDetails additionalDetails;
		if (existingDetails == null)
		{
			// Create new additional details entity
			additionalDetails = new EmployeeAdditionalDetails(
				id: guidGenerator.Create(),
				tenantId: CurrentTenant.Id,
				employeeId: employeeId,
				bplCardNo: input.BplCardNo,
				personWithDisability: input.PersonWithDisability,
				secondLanguage: input.SecondLanguage,
				libraryMember: input.LibraryMember,
				transportMember: input.TransportMember);

			await additionalDetailsRepository.InsertAsync(additionalDetails);
		}
		else
		{
			// Update existing additional details
			existingDetails.Update(
				bplCardNo: input.BplCardNo,
				personWithDisability: input.PersonWithDisability,
				secondLanguage: input.SecondLanguage,
				libraryMember: input.LibraryMember,
				transportMember: input.TransportMember);

			await additionalDetailsRepository.UpdateAsync(existingDetails);
			additionalDetails = existingDetails;
		}

		// Return success response
		return new EmployeeAdditionalDetailsDto
		{
			EmployeeCode = employee.EmployeeCode,
			BplCardNo = additionalDetails.BplCardNo,
			PersonWithDisability = additionalDetails.PersonWithDisability,
			SecondLanguage = additionalDetails.SecondLanguage,
			LibraryMember = additionalDetails.LibraryMember,
			TransportMember = additionalDetails.TransportMember
		};
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
		if (input?.PayrollStructures == null || input.PayrollStructures.Count == 0)
		{
			throw new UserFriendlyException("Payroll structure is required.");
		}

		var payrollStructureId = input.PayrollStructures[0].Id;
		await payrollStructureManager.SetEmployeePayrollStructureAsync(employeeId, payrollStructureId);
	}

	public async Task DeleteDocumentAsync(Guid employeeId, Guid documentId)
	{
		await employeeManager.DeleteDocumentAsync(employeeId, documentId);
	}

	public async Task<string> UploadProfilePictureAsync(Guid employeeId, string fileName, byte[] fileContent)
	{
		return await employeeManager.UploadProfilePictureAsync(employeeId, fileName, fileContent);
	}

	public async Task<DocumentDto?> GetProfilePictureAsync(Guid employeeId)
	{
		var employee = await employeeRepository.GetAsync(employeeId);
		
		if (string.IsNullOrEmpty(employee.ProfilePictureBlobName))
		{
			return null;
		}

		try
		{
			await using var stream = await blobContainer.GetAsync(employee.ProfilePictureBlobName);
			await using var memoryStream = new MemoryStream();
			await stream.CopyToAsync(memoryStream);

			// Determine content type from file extension
			var contentType = GetContentTypeFromFileName(employee.ProfilePictureBlobName);

			return new DocumentDto
			{
				FileName = employee.ProfilePictureBlobName,
				ContentType = contentType,
				Content = memoryStream.ToArray(),
				Size = memoryStream.Length
			};
		}
		catch (FileNotFoundException)
		{
			// Blob was deleted externally or doesn't exist - this is expected behavior
			logger.LogWarning(
				"Profile picture blob not found for employee {EmployeeId}. BlobName: {BlobName}",
				employeeId,
				employee.ProfilePictureBlobName);
			return null;
		}
		catch (Exception ex) when (ex.GetType().Name == "BlobProviderNotFoundException" || ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
		{
			// Handle ABP BlobStoring provider-specific "not found" exceptions
			logger.LogWarning(
				"Profile picture blob not found for employee {EmployeeId}. BlobName: {BlobName}. Exception: {ExceptionType}",
				employeeId,
				employee.ProfilePictureBlobName,
				ex.GetType().Name);
			return null;
		}
		catch (Exception ex)
		{
			// Log unexpected errors and rethrow to surface operational issues
			logger.LogError(
				ex,
				"Unexpected error retrieving profile picture for employee {EmployeeId}. BlobName: {BlobName}",
				employeeId,
				employee.ProfilePictureBlobName);
			throw;
		}
	}

	private static string GetContentTypeFromFileName(string fileName)
	{
		var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
		
		return extension switch
		{
			".jpg" or ".jpeg" => "image/jpeg",
			".png" => "image/png",
			".gif" => "image/gif",
			".bmp" => "image/bmp",
			".webp" => "image/webp",
			".svg" => "image/svg+xml",
			".ico" => "image/x-icon",
			_ => "application/octet-stream" // Fallback for unknown types
		};
	}

	public async Task<EmployeePayrollDetailsDto> GetPayrollDetailsAsync(Guid employeeId)
	{
		var employee = await employeeRepository.GetAsync(employeeId);
		var payrollDetails = await payrollDetailsRepository.GetByEmployeeIdAsync(employeeId);
		var earnings = await payrollComponentRepository.GetEarningsByEmployeeIdAsync(employeeId);
		var deductions = await payrollComponentRepository.GetDeductionsByEmployeeIdAsync(employeeId);

		var result = new EmployeePayrollDetailsDto
		{
			// Note: EmployeeId property holds the employee code (string), not the GUID
			EmployeeId = employee.EmployeeCode,
			PayrollDetails = payrollDetails != null ? new PayrollDetailsDto
			{
				BankName = payrollDetails.BankName,
				BranchName = payrollDetails.BranchName,
				AccountHolderName = payrollDetails.AccountHolderName,
				AccountNumber = payrollDetails.AccountNumber,
				IfscCode = payrollDetails.IfscCode,
				PfRule = payrollDetails.PfRule,
				EpfNumber = payrollDetails.EpfNumber,
				UanNumber = payrollDetails.UanNumber,
				PunchId = payrollDetails.PunchId,
				BasicSalary = payrollDetails.BasicSalary,
				TdsPercentage = payrollDetails.TdsPercentage,
				TdsAmount = payrollDetails.TdsAmount
			} : new PayrollDetailsDto(),
		Earnings = (earnings ?? Enumerable.Empty<EmployeePayrollComponent>()).Select(e => new PayrollComponentDto
		{
			ComponentName = e.ComponentName,
			Type = e.Type,
			Amount = e.Amount
		}).ToList(),
		Deductions = (deductions ?? Enumerable.Empty<EmployeePayrollComponent>()).Select(d => new PayrollComponentDto
		{
			ComponentName = d.ComponentName,
			Type = d.Type,
			Amount = d.Amount
		}).ToList()
		};

		return result;
	}

	public async Task UpdatePayrollDetailsAsync(Guid employeeId, UpdateEmployeePayrollDetailsDto input)
	{
		// Validate input
		if (input.PayrollDetails == null)
		{
			throw new UserFriendlyException("Payroll details are required.");
		}

		// Validate employee exists (throws EntityNotFoundException if not found)
		await employeeRepository.GetAsync(employeeId);
		
		var payrollDetails = await payrollDetailsRepository.GetByEmployeeIdAsync(employeeId);

		if (payrollDetails == null)
		{
			payrollDetails = new EmployeePayrollDetails(guidGenerator.Create(), employeeId);
			payrollDetails.UpdateDetails(
				input.PayrollDetails.BankName,
				input.PayrollDetails.BranchName,
				input.PayrollDetails.AccountHolderName,
				input.PayrollDetails.AccountNumber,
				input.PayrollDetails.IfscCode,
				input.PayrollDetails.PfRule,
				input.PayrollDetails.EpfNumber,
				input.PayrollDetails.UanNumber,
				input.PayrollDetails.PunchId,
				input.PayrollDetails.BasicSalary,
				input.PayrollDetails.TdsPercentage,
				input.PayrollDetails.TdsAmount
			);
			await payrollDetailsRepository.InsertAsync(payrollDetails);
		}
		else
		{
			payrollDetails.UpdateDetails(
				input.PayrollDetails.BankName,
				input.PayrollDetails.BranchName,
				input.PayrollDetails.AccountHolderName,
				input.PayrollDetails.AccountNumber,
				input.PayrollDetails.IfscCode,
				input.PayrollDetails.PfRule,
				input.PayrollDetails.EpfNumber,
				input.PayrollDetails.UanNumber,
				input.PayrollDetails.PunchId,
				input.PayrollDetails.BasicSalary,
				input.PayrollDetails.TdsPercentage,
				input.PayrollDetails.TdsAmount
			);
			await payrollDetailsRepository.UpdateAsync(payrollDetails);
		}

		await payrollComponentRepository.DeleteByEmployeeIdAsync(employeeId, isEarning: true);
		await payrollComponentRepository.DeleteByEmployeeIdAsync(employeeId, isEarning: false);

		foreach (var earning in input.Earnings ?? Enumerable.Empty<PayrollComponentDto>())
		{
			var component = new EmployeePayrollComponent(
				guidGenerator.Create(),
				employeeId,
				earning.ComponentName,
				earning.Type,
				earning.Amount,
				isEarning: true
			);
			await payrollComponentRepository.InsertAsync(component);
		}

		foreach (var deduction in input.Deductions ?? Enumerable.Empty<PayrollComponentDto>())
		{
			var component = new EmployeePayrollComponent(
				guidGenerator.Create(),
				employeeId,
				deduction.ComponentName,
				deduction.Type,
				deduction.Amount,
				isEarning: false
			);
			await payrollComponentRepository.InsertAsync(component);
		}

		await employeeCacheManager.InvalidateEmployeeAsync(employeeId);
	}

}
