using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using ShikshaMitra.Common;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;
using Volo.Abp.Identity;

namespace ShikshaMitra.HR.Employees;

public class EmployeeManager(
	IEmployeeRepository employeeRepository,
	IGuidGenerator guidGenerator,
	IIdentityUserRepository userRepository,
	IIdentityRoleRepository roleRepository,
	IBlobContainer blobContainer,
	IEmployeeDocumentRepository employeeDocumentRepository,
	UserManager<IdentityUser> userManager,
	IEducationMediumStageRepository educationMediumStageRepository,
	IRepository<EducationBoard, Guid> educationBoardRepository,
	IRepository<EducationMedium, Guid> educationMediumRepository
) : DomainService
{
	// Private helper method to get an employee by ID
	private async Task<Employee> GetEmployeeAsync(Guid employeeId)
	{
		var employee = await employeeRepository.GetAsync(employeeId);
		return employee
			?? throw new BusinessException($"Employee with ID {employeeId} does not exist.");
	}

	// Private helper method to get a user by ID
	private async Task<IdentityUser> GetUserAsync(Guid userId)
	{
		var user = await userRepository.GetAsync(userId);
		return user ?? throw new BusinessException($"User with ID {userId} does not exist.");
	}

	// Private helper method to get a role by ID
	private async Task<IdentityRole> GetRoleAsync(Guid roleId)
	{
		var role = await roleRepository.GetAsync(roleId);
		return role ?? throw new BusinessException($"Role with ID {roleId} does not exist.");
	}

	public async Task<Employee> CreateEmployeeAsync(
		Guid roleId,
		Guid educationMediumStageId,
		string firstName,
		string lastName,
		string? middleName,
		string shortName,
		EmployeeStatusType employeeStatusType,
		DateOnly? dateOfBirth,
		string? familyCast = null,
		CastType? castType = null,
		GenderType? genderType = null,
		string? religion = null,
		MaritalStatusType? maritalStatus = null,
		string? bloodGroup = null,
		string? mobileNo = null,
		string? secondaryMobileNo = null,
		string? email = null,
		string? address1 = null,
		string? address2 = null,
		string? address3 = null,
		string? department = null,
		string? designation = null,
		DateOnly? dateOfJoining = null,
		EmployeeGroupType? employeeGroup = null,
		string? qualification = null,
		string? identificationMark1 = null,
		string? city = null,
		State? state = null,
		Country? country = null,
		string? pinCode = null,
		Guid? shiftId = null
	)
	{
		// Validate role existence
		IdentityRole role = await GetRoleAsync(roleId);

		// i have educationMediumStageId, it stores educationMediumId, educationMedium table stores educationBoardId, I want boardName from educationBoard table using educationMediumStageId in ef core
		var stages = await educationMediumStageRepository.GetQueryableAsync();
		var mediums = await educationMediumRepository.GetQueryableAsync();
		var boards = await educationBoardRepository.GetQueryableAsync();

		var query =
			from stage in stages
			join medium in mediums on stage.EducationMediumId equals medium.Id
			join board in boards on medium.EducationBoardId equals board.Id
			where stage.Id == educationMediumStageId
			select new // Creates a new anonymous object, NOT an EducationMediumStage
			{
				StageId = stage.Id,
				StageName = stage.Name,
				MediumName = medium.Name,
				BoardType = board.Type,
			};

		// 3. Execute the query
		var boardType = await AsyncExecuter.FirstOrDefaultAsync(query);

		// Generate employee code
		var employeeCode = await GenerateEmployeeCodeAsync(
			boardType!.BoardType,
			dateOfJoining ?? DateOnly.FromDateTime(DateTime.Now),
			role.Name
		);

		// Create employee entity
		var employee = new Employee(
			id: guidGenerator.Create(),
			tenantId: CurrentTenant.Id,
			userId: null,
			profilePictureBlobName: null,
			employeeCode: employeeCode,
			lastName: lastName,
			firstName: firstName,
			middleName: middleName,
			shortName: shortName,
			employeeStatusType: employeeStatusType,
			dateOfBirth: dateOfBirth,
			familyCast: familyCast,
			castType: castType,
			genderType: genderType,
			religion: religion,
			maritalStatus: maritalStatus,
			bloodGroup: bloodGroup,
			mobileNo: mobileNo,
			secondaryMobileNo: secondaryMobileNo,
			email: email,
			address1: address1,
			address2: address2,
			address3: address3,
			department: department,
			designation: designation,
			dateOfJoining: dateOfJoining,
			employeeGroup: employeeGroup,
			qualification: qualification,
			roleId: roleId,
			identificationMark1: identificationMark1,
			city: city,
			state: state,
			country: country,
			pinCode: pinCode,
			educationMediumStageId: educationMediumStageId,
			fatherDetails: new ContactDetails(
				name: null,
				occupation: null,
				contactNo: null,
				address1: null,
				address2: null,
				address3: null,
				city: null,
				stateName: null,
				stateIsoCode: null,
				countryName: null,
				countryIsoCode: null,
				pinCode: null
			),
			motherDetails: new ContactDetails(
				name: null,
				occupation: null,
				contactNo: null,
				address1: null,
				address2: null,
				address3: null,
				city: null,
				stateName: null,
				stateIsoCode: null,
				countryName: null,
				countryIsoCode: null,
				pinCode: null
			),
			spouseDetails: new ContactDetails(
				name: null,
				occupation: null,
				contactNo: null,
				address1: null,
				address2: null,
				address3: null,
				city: null,
				stateName: null,
				stateIsoCode: null,
				countryName: null,
				countryIsoCode: null,
				pinCode: null
			),
			bankDetails: new BankDetails(
				bankName: null,
				branchName: null,
				accountHolderName: null,
				accountNumber: null,
				ifscCode: null
			),
			workExperiences: null,
			shiftId: shiftId
		);

		// 1. Create the IdentityUser
		var user = new IdentityUser(guidGenerator.Create(), email, email, CurrentTenant.Id);
		user.SetPhoneNumber(mobileNo, false);
		user.Name = firstName;
		user.Surname = lastName;
		var password = "Temp@123"; // Implement a method to generate a random password
		var userCreateResult = await userManager.CreateAsync(user, password);

		if (!userCreateResult.Succeeded)
		{
			// Handle user creation errors (e.g., duplicate email, weak password)
			throw new UserFriendlyException(
				"Failed to create user: "
					+ string.Join(", ", userCreateResult.Errors.Select(e => e.Description))
			);
		}

		// 2. Assign the role to the user
		var roleAddResult = await userManager.AddToRoleAsync(user, role.Name);
		if (!roleAddResult.Succeeded)
		{
			// Handle role assignment errors
			await userManager.DeleteAsync(user); // Rollback user creation
			throw new UserFriendlyException(
				"Failed to add user to role: "
					+ string.Join(", ", roleAddResult.Errors.Select(e => e.Description))
			);
		}

		employee.UpdateUserId(user.Id);
		return employee;
	}

	public async Task<Employee> UpdateEmployeeAsync(
		Guid employeeId,
		Guid educationMediumStageId,
		string firstName,
		string lastName,
		string? middleName,
		string shortName,
		EmployeeStatusType employeeStatusType,
		DateOnly? dateOfBirth,
		string? familyCast = null,
		CastType? castType = null,
		GenderType? genderType = null,
		string? religion = null,
		MaritalStatusType? maritalStatus = null,
		string? bloodGroup = null,
		string? mobileNo = null,
		string? secondaryMobileNo = null,
		string? email = null,
		string? address1 = null,
		string? address2 = null,
		string? address3 = null,
		string? department = null,
		string? designation = null,
		DateOnly? dateOfJoining = null,
		EmployeeGroupType? employeeGroup = null,
		string? qualification = null,
		Guid? roleId = null,
		string? identificationMark1 = null,
		string? city = null,
		State? state = null,
		Country? country = null,
		string? pinCode = null,
		Guid? shiftId = null
	)
	{
		// Get the employee
		Employee employee = await GetEmployeeAsync(employeeId);

		// Update user roles if roleId is provided and changed
		if (roleId.HasValue && employee.RoleId != roleId.Value && employee.UserId != null)
		{
			// Get the associated user
			IdentityUser user = await GetUserAsync((Guid)employee.UserId);

			// Remove the user from the old role
			IdentityRole? oldRole = await roleRepository.GetAsync(employee.RoleId);
			IdentityResult? removeResult = await userManager.RemoveFromRoleAsync(
				user,
				oldRole.Name
			);
			// Add the user to the new role
			IdentityRole? newRole = await roleRepository.GetAsync(roleId.Value);
			IdentityResult? addResult = await userManager.AddToRoleAsync(user, newRole.Name);
		}

		// Update the employee
		employee.UpdateBasicInfo(
			lastName: lastName,
			firstName: firstName,
			middleName: middleName,
			shortName: shortName,
			employeeStatusType: employeeStatusType,
			dateOfBirth: dateOfBirth,
			profilePictureBlobName: employee.ProfilePictureBlobName, // Preserve existing profile picture
			familyCast: familyCast,
			castType: castType,
			genderType: genderType,
			religion: religion,
			maritalStatus: maritalStatus,
			bloodGroup: bloodGroup,
			mobileNo: mobileNo,
			secondaryMobileNo: secondaryMobileNo,
			email: email,
			address1: address1,
			address2: address2,
			address3: address3,
			department: department,
			designation: designation,
			dateOfJoining: dateOfJoining,
			employeeGroup: employeeGroup,
			qualification: qualification,
			roleId: roleId,
			identificationMark1: identificationMark1,
			city: city,
			state: state,
			country: country,
			pinCode: pinCode,
			educationMediumStageId: educationMediumStageId,
			shiftId: shiftId
		);
		// Validate the updated employee if needed
		ValidateEmployee(employee);

		return employee;
	}

	public async Task<Employee> UpdateEmployeeStatusAsync(
		Guid employeeId,
		EmployeeStatusType employeeStatusType,
		string? changeReason
	)
	{
		// Get the employee
		var employee = await GetEmployeeAsync(employeeId);

		// Update the employee status
		employee = employee.UpdateEmployeeStatus(employeeStatusType, changeReason);
		return employee;
	}

	private static void ValidateEmployee(Employee employee)
	{
		// Add any domain-specific validation rules here

		// Example validations:
		if (employee.DateOfBirth >= DateOnly.FromDateTime(DateTime.UtcNow))
		{
			throw new BusinessException("Date of birth cannot be in the future");
		}

		if (employee.DateOfJoining.HasValue && employee.DateOfJoining.Value <= employee.DateOfBirth)
		{
			throw new BusinessException("Date of joining must be after date of birth");
		}

		// Add more validations as needed
	}

	public async Task<Employee> UpdateFamilyInfoByEmployeeIdAsync(
		Guid employeeId,
		ContactDetails fatherDetails,
		ContactDetails motherDetails,
		ContactDetails spouseDetails
	)
	{
		// Get the employee
		var employee = await GetEmployeeAsync(employeeId);

		// Update the family details
		employee.UpdateFamilyDetails(fatherDetails, motherDetails, spouseDetails);

		return employee;
	}

	public async Task<Employee> UpdateExperienceInfoByEmployeeIdAsync(
		Guid employeeId,
		List<WorkExperience> workExperiences
	)
	{
		// Get the employee
		var employee = await GetEmployeeAsync(employeeId);

		// Update the work experiences
		employee.UpdateWorkExperiences(workExperiences);
		return employee;
	}

	public async Task<Employee> UpdateBankDetailsByEmployeeIdAsync(
		Guid employeeId,
		BankDetails bankDetails
	)
	{
		// Get the employee
		var employee = await GetEmployeeAsync(employeeId);

		// Update the bank details
		employee.UpdateBankDetails(bankDetails);
		return employee;
	}

	public async Task<Employee> UpdateAdditionalDetailsByEmployeeIdAsync(
		Guid employeeId,
		EmployeeAdditionalDetails additionalDetails
	)
	{
		// Get the employee
		var employee = await GetEmployeeAsync(employeeId);

		// Update the additional details
		employee.UpdateAdditionalDetails(additionalDetails);
		return employee;
	}

	public async Task AddDocumentsAsync(Guid employeeId, List<EmployeeDocumentData> documents)
	{
		var employee = await employeeRepository.GetAsync(employeeId);

		foreach (var docData in documents)
		{
			var documentId = GuidGenerator.Create();
			var blobName = $"{documentId}_{docData.FileName}";

			// Save the file content to blob storage
			await blobContainer.SaveAsync(blobName, docData.Content, overrideExisting: true);

			// Create and save the EmployeeDocument entity (metadata)
			var document = new EmployeeDocument(
				id: documentId,
				tenantId: CurrentTenant.Id,
				employeeId: employeeId,
				documentName: docData.FileName,
				contentType: docData.ContentType,
				size: docData.Size,
				blobName: blobName
			);

			await employeeDocumentRepository.InsertAsync(document);

			// Associate the document with the employee, if needed
			employee.AddDocument(document);
		}

		// Update the employee entity
		await employeeRepository.UpdateAsync(employee);
	}

	public async Task<EmployeeDocument> GetDocumentAsync(Guid employeeId, Guid documentId)
	{
		var document = await employeeDocumentRepository.FirstOrDefaultAsync(d =>
			d.EmployeeId == employeeId && d.Id == documentId
		);
		return document
			?? throw new BusinessException($"Document with ID {documentId} does not exist.");
	}

	public async Task<List<EmployeeDocument>> GetDocumentsAsync(Guid employeeId)
	{
		List<EmployeeDocument> documents = await employeeDocumentRepository.GetListAsync(d =>
			d.EmployeeId == employeeId
		);
		return documents;
	}

	public async Task<EmployeeDocument> UpdateDocumentAsync(
		Guid employeeId,
		Guid documentId,
		string fileName,
		string contentType,
		long size,
		byte[] content
	)
	{
		EmployeeDocument document = await GetDocumentAsync(employeeId, documentId);

		// Update blob
		await blobContainer.SaveAsync(document.BlobName, content, overrideExisting: true);

		// Update metadata
		document.UpdateDocumentMetadata(fileName, contentType, size);

		await employeeDocumentRepository.UpdateAsync(document);
		return document;
	}

	public async Task DeleteDocumentAsync(Guid employeeId, Guid documentId)
	{
		EmployeeDocument document = await GetDocumentAsync(employeeId, documentId);

		// Delete blob from storage
		await blobContainer.DeleteAsync(document.BlobName);

		// Delete document metadata
		// Note: The Documents collection is an owned entity, so EF Core handles the relationship cleanup automatically
		await employeeDocumentRepository.DeleteAsync(document);
	}

	public async Task<string> UploadProfilePictureAsync(Guid employeeId, string fileName, byte[] fileContent)
	{
		// Domain-level validation guards
		ValidateProfilePictureUpload(fileName, fileContent);

		var employee = await GetEmployeeAsync(employeeId);

		// Delete old profile picture if exists
		if (!string.IsNullOrEmpty(employee.ProfilePictureBlobName))
		{
			await blobContainer.DeleteAsync(employee.ProfilePictureBlobName);
		}

		// Generate new blob name
		var blobName = $"profile-pictures/{employeeId}_{fileName}";

		// Save to blob storage
		await blobContainer.SaveAsync(blobName, fileContent, overrideExisting: true);

		// Update employee with new blob name
		employee.UpdateProfilePicture(blobName);
		await employeeRepository.UpdateAsync(employee);

		return blobName;
	}

	private static void ValidateProfilePictureUpload(string fileName, byte[] fileContent)
	{
		// Validate file name
		if (string.IsNullOrWhiteSpace(fileName))
		{
			throw new BusinessException("File name cannot be empty.");
		}

		// Validate file content
		if (fileContent == null || fileContent.Length == 0)
		{
			throw new BusinessException("File content cannot be empty.");
		}

		// Validate file size (max 5MB)
		const long maxFileSizeBytes = 5 * 1024 * 1024; // 5MB
		if (fileContent.Length > maxFileSizeBytes)
		{
			throw new BusinessException($"File size cannot exceed {maxFileSizeBytes / 1024 / 1024}MB. Current size: {fileContent.Length / 1024 / 1024}MB.");
		}

		// Validate file type by extension
		var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
		var fileExtension = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
		
		if (string.IsNullOrEmpty(fileExtension) || !allowedExtensions.Contains(fileExtension))
		{
			throw new BusinessException($"Invalid file type. Allowed types: {string.Join(", ", allowedExtensions)}. Provided: {fileExtension}");
		}

		// Validate file signature (magic bytes) for additional security
		if (!IsValidImageFile(fileContent))
		{
			throw new BusinessException("Invalid image file. The file content does not match a valid image format.");
		}
	}

	private static bool IsValidImageFile(byte[] fileContent)
	{
		if (fileContent.Length < 4)
		{
			return false;
		}

		// Check magic bytes for common image formats
		// JPEG: FF D8 FF
		if (fileContent[0] == 0xFF && fileContent[1] == 0xD8 && fileContent[2] == 0xFF)
		{
			return true;
		}

		// PNG: 89 50 4E 47
		if (fileContent[0] == 0x89 && fileContent[1] == 0x50 && fileContent[2] == 0x4E && fileContent[3] == 0x47)
		{
			return true;
		}

		// GIF: 47 49 46 38
		if (fileContent[0] == 0x47 && fileContent[1] == 0x49 && fileContent[2] == 0x46 && fileContent[3] == 0x38)
		{
			return true;
		}

		// WebP: RIFF....WEBP (check for RIFF and WEBP signatures)
		if (fileContent.Length >= 12 &&
		    fileContent[0] == 0x52 && fileContent[1] == 0x49 && fileContent[2] == 0x46 && fileContent[3] == 0x46 && // RIFF
		    fileContent[8] == 0x57 && fileContent[9] == 0x45 && fileContent[10] == 0x42 && fileContent[11] == 0x50) // WEBP
		{
			return true;
		}

		return false;
	}

	private async Task<string> GenerateEmployeeCodeAsync(
		EducationBoardType boardType,
		DateOnly dateOfJoining,
		string roleName
	)
	{
		// Get the last EmployeeCode
		Employee? lastEmployee = await employeeRepository.GetLastEmployeeAsync();

		int nextNumber = 1;

		if (lastEmployee != null)
		{
			var lastCode = lastEmployee.EmployeeCode;

			// Extract the numeric part from the last code (e.g., "EMP001" -> 1)
			if (int.TryParse(lastCode[3..], out int lastNumber))
			{
				nextNumber = lastNumber + 1;
			}
		}

		// board - role - joining year - sequence number
		return $"{boardType.ToString().Substring(0, 2)}{roleName.Substring(0, 3)}{dateOfJoining.Year.ToString().Substring(2)}{nextNumber:D3}";
	}
}
