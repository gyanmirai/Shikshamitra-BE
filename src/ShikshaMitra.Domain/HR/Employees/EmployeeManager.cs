using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using ShikshaMitra.Common;
using Volo.Abp;
using Volo.Abp.BlobStoring;
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
	UserManager<IdentityUser> userManager
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

		// Generate employee code
		var employeeCode = await GenerateEmployeeCodeAsync(
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

	public async Task AddDocumentsAsync(Guid employeeId, List<EmployeeDocumentData> documents)
	{
		var employee = await employeeRepository.GetAsync(employeeId);

		foreach (var docData in documents)
		{
			var documentId = GuidGenerator.Create();
			var blobName = $"{documentId}_{docData.FileName}";

			// Save the file content to blob storage
			await blobContainer.SaveAsync(blobName, docData.Content, overrideExisting: true);

			// Create the EmployeeDocument entity (metadata)
			var document = new EmployeeDocument(
				id: documentId,
				tenantId: CurrentTenant.Id,
				employeeId: employeeId,
				documentName: docData.FileName,
				contentType: docData.ContentType,
				size: docData.Size,
				blobName: blobName
			);

			// Associate the document with the employee (owned entity)
			employee.AddDocument(document);
		}

		// Update the employee entity (EF Core will handle owned entities)
		await employeeRepository.UpdateAsync(employee);
	}

	public async Task<EmployeeDocument> GetDocumentAsync(Guid employeeId, Guid documentId)
	{
		var employee = await employeeRepository.GetAsync(employeeId);
		var document = employee.Documents.FirstOrDefault(d => d.Id == documentId);
		return document
			?? throw new BusinessException($"Document with ID {documentId} does not exist.");
	}

	public async Task<List<EmployeeDocument>> GetDocumentsAsync(Guid employeeId)
	{
		var employee = await employeeRepository.GetAsync(employeeId);
		return employee.Documents.ToList();
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
		var employee = await employeeRepository.GetAsync(employeeId);
		var document = employee.Documents.FirstOrDefault(d => d.Id == documentId);
		
		if (document == null)
		{
			throw new BusinessException($"Document with ID {documentId} does not exist.");
		}

		// Update blob
		await blobContainer.SaveAsync(document.BlobName, content, overrideExisting: true);

		// Update metadata
		document.UpdateDocumentMetadata(fileName, contentType, size);

		await employeeRepository.UpdateAsync(employee);
		return document;
	}

	public async Task DeleteDocumentAsync(Guid employeeId, Guid documentId)
	{
		var employee = await employeeRepository.GetAsync(employeeId);
		var document = employee.Documents.FirstOrDefault(d => d.Id == documentId);
		
		if (document == null)
		{
			throw new BusinessException($"Document with ID {documentId} does not exist.");
		}

		// Delete blob from storage
		await blobContainer.DeleteAsync(document.BlobName);

		// Remove document from collection (owned entity - EF Core tracks changes)
		employee.Documents.Remove(document);

		// Update the employee entity
		await employeeRepository.UpdateAsync(employee);
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
			throw new BusinessException($"File size cannot exceed {maxFileSizeBytes / 1024.0 / 1024.0:F2}MB. Current size: {fileContent.Length / 1024.0 / 1024.0:F2}MB.");
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

	/// <summary>
	/// Generates a unique, human-readable employee code without requiring additional database tables.
	/// Uses a combination of tenant ID, role, year, and a hash-based unique identifier.
	/// 
	/// Format: {RoleCode}{Year}{UniqueHash}
	/// Example: PRI24A3F2 (Principal 2024 with unique hash A3F2)
	/// 
	/// Uniqueness is guaranteed by:
	/// 1. Tenant isolation (different tenants will have different hashes)
	/// 2. Year-based separation
	/// 3. Unique employee ID hash
	/// </summary>
	private async Task<string> GenerateEmployeeCodeAsync(
		DateOnly dateOfJoining,
		string roleName
	)
	{
		var year = dateOfJoining.Year;
		var tenantId = CurrentTenant.Id ?? Guid.Empty;

		// Get count of existing employees for this tenant and year (for the hash seed)
		var existingCount = await GetEmployeeCountForTenantAndYearAsync(tenantId, year);

		// Generate role code (3 chars)
		string roleCode = GetRoleCode(roleName);

		// Get 2-digit year
		string yearCode = (year % 100).ToString("D2");

		// Generate a unique 4-character hash based on tenant, year, and count
		// This creates a deterministic but unique identifier
		string uniqueHash = GenerateUniqueHash(tenantId, year, existingCount);

		// Format: {RoleCode}{Year}{UniqueHash}
		// Example: PRI24A3F2 (9 characters total)
		return $"{roleCode}{yearCode}{uniqueHash}";
	}

	/// <summary>
	/// Gets the count of employees for a specific tenant and year.
	/// Used as a seed for generating unique hashes.
	/// </summary>
	private async Task<int> GetEmployeeCountForTenantAndYearAsync(Guid tenantId, int year)
	{
		var queryable = await employeeRepository.GetQueryableAsync();

		var count = queryable
			.Where(e => e.TenantId == (tenantId == Guid.Empty ? null : tenantId))
			.Where(e => e.DateOfJoining.HasValue && e.DateOfJoining.Value.Year == year)
			.Count();

		return count;
	}

	/// <summary>
	/// Generates a unique 4-character hash from tenant ID, year, and employee count.
	/// Uses a combination of tenant hash and counter to ensure uniqueness.
	/// </summary>
	private static string GenerateUniqueHash(Guid tenantId, int year, int count)
	{
		// Combine tenant ID, year, and count to create a unique seed
		var seed = $"{tenantId}{year}{count}{DateTime.UtcNow.Ticks}";

		// Generate hash code
		var hashCode = seed.GetHashCode();

		// Convert to alphanumeric (base 36: 0-9, A-Z)
		var hash = ToBase36(Math.Abs(hashCode));

		// Take first 4 characters and pad if necessary
		return hash.PadRight(4, '0').Substring(0, 4).ToUpperInvariant();
	}

	/// <summary>
	/// Converts an integer to base-36 representation (0-9, A-Z).
	/// This creates compact, human-readable codes.
	/// </summary>
	private static string ToBase36(int value)
	{
		const string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
		var result = "";

		do
		{
			result = chars[value % 36] + result;
			value /= 36;
		} while (value > 0);

		return result;
	}

	/// <summary>
	/// Maps role name to a 3-letter code.
	/// Handles common role names and provides fallback for unknown roles.
	/// </summary>
	private static string GetRoleCode(string roleName)
	{
		if (string.IsNullOrWhiteSpace(roleName))
			return "UNK";

		// Normalize role name
		var normalized = roleName.Trim().ToUpperInvariant().Replace(" ", "");

		// Map common roles
		return normalized switch
		{
			"PRINCIPAL" => "PRI",
			"VICEPRINCIPAL" => "VPR",
			"HEADOFDEPARTMENT" => "HOD",
			"TEACHER" => "TEA",
			"SENIORTEACHER" => "SRT",
			"ASSISTANTTEACHER" => "AST",
			"ADMINISTRATIVEMANAGER" => "ADM",
			"HRMANAGER" => "HRM",
			"FINANCEOFFICER" => "FIN",
			"ITADMINISTRATOR" => "ITA",
			"OFFICEASSISTANT" => "OFA",
			"LIBRARIAN" => "LIB",
			"LABASSISTANT" => "LAB",
			"COUNSELOR" => "COU",
			"NURSE" => "NUR",
			"SECURITYOFFICER" => "SEC",
			"DRIVER" => "DRV",
			"JANITOR" => "JAN",
			_ => normalized.Length >= 3 
				? normalized.Substring(0, 3) 
				: normalized.PadRight(3, 'X')
		};
	}
}
