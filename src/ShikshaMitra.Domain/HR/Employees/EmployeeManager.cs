using ShikshaMitra.Common;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
	IEmployeeDocumentRepository employeeDocumentRepository
) : DomainService
{
	// Private helper method to get an employee by ID
	private async Task<Employee> GetEmployeeAsync(Guid employeeId)
	{
		var employee = await employeeRepository.GetAsync(employeeId);
		return employee ?? throw new BusinessException($"Employee with ID {employeeId} does not exist.");
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
		Guid userId,
		Guid roleId,
		string firstName,
		string lastName,
		string? middleName,
		string shortName,
		EmployeeStatusType employeeStatusType,
		DateOnly? dateOfBirth,
		byte[]? profilePicture = null,
		string? familyCast = null,
		EmployeeCastType? castType = null,
		GenderType? genderType = null,
		string? religion = null,
		MaritalStatusType? maritalStatus = null,
		string? bloodGroup = null,
		string? mobileNo = null,
		string? email = null,
		string? address = null,
		string? department = null,
		string? designation = null,
		DateOnly? dateOfJoining = null,
		EmployeeGroupType? employeeGroup = null,
		string? qualification = null,
		string? identificationMark1 = null,
		string? city = null,
		string? state = null,
		string? pinCode = null)
	{
		// Validate user existence
		await GetUserAsync(userId);

		// Validate role existence
		await GetRoleAsync(roleId);

		// Generate employee code
		string employeeCode = await GenerateEmployeeCodeAsync();

		// Create employee entity
		var employee = new Employee(
			id: guidGenerator.Create(),
			tenantId: CurrentTenant.Id,
			userId: userId,
			profilePicture: profilePicture,
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
			email: email,
			address: address,
			department: department,
			designation: designation,
			dateOfJoining: dateOfJoining,
			employeeGroup: employeeGroup,
			qualification: qualification,
			roleId: roleId,
			identificationMark1: identificationMark1,
			city: city,
			state: state,
			pinCode: pinCode,
			fatherDetails:
			new ContactDetails(
				name: null,
				occupation: null,
				contactNo: null,
				address: null,
				city: null,
				state: null,
				pinCode: null),
			motherDetails: new ContactDetails(
				name: null,
				occupation: null,
				contactNo: null,
				address: null,
				city: null,
				state: null,
				pinCode: null),
			spouseDetails: new ContactDetails(
				name: null,
				occupation: null,
				contactNo: null,
				address: null,
				city: null,
				state: null,
				pinCode: null),
			bankDetails: new BankDetails(
				bankName: null,
				branchName: null,
				accountHolderName: null,
				accountNumber: null,
				ifscCode: null
			),
			workExperiences: null
		);

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
		byte[]? profilePicture = null,
		string? familyCast = null,
		EmployeeCastType? castType = null,
		GenderType? genderType = null,
		string? religion = null,
		MaritalStatusType? maritalStatus = null,
		string? bloodGroup = null,
		string? mobileNo = null,
		string? email = null,
		string? address = null,
		string? department = null,
		string? designation = null,
		DateOnly? dateOfJoining = null,
		EmployeeGroupType? employeeGroup = null,
		string? qualification = null,
		Guid? roleId = null,
		string? identificationMark1 = null,
		string? city = null,
		string? state = null,
		string? pinCode = null)
	{
		// Get the employee
		Employee employee = await GetEmployeeAsync(employeeId);

		// Update the employee
		employee.UpdateBasicInfo(
			lastName: lastName,
			firstName: firstName,
			middleName: middleName,
			shortName: shortName,
			employeeStatusType: employeeStatusType,
			dateOfBirth: dateOfBirth,
			profilePicture: profilePicture,
			familyCast: familyCast,
			castType: castType,
			genderType: genderType,
			religion: religion,
			maritalStatus: maritalStatus,
			bloodGroup: bloodGroup,
			mobileNo: mobileNo,
			email: email,
			address: address,
			department: department,
			designation: designation,
			dateOfJoining: dateOfJoining,
			employeeGroup: employeeGroup,
			qualification: qualification,
			roleId: roleId,
			identificationMark1: identificationMark1,
			city: city,
			state: state,
			pinCode: pinCode
		);

		// Validate the updated employee if needed
		ValidateEmployee(employee);

		return employee;
	}

	public async Task<Employee> UpdateEmployeeStatusAsync(Guid employeeId, EmployeeStatusType employeeStatusType)
	{
		// Get the employee
		var employee = await GetEmployeeAsync(employeeId);

		// Update the employee status
		employee = employee.UpdateEmployeeStatus(employeeStatusType);
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

	public async Task<Employee> UpdateFamilyInfoByEmployeeIdAsync(Guid employeeId, ContactDetails fatherDetails,
		ContactDetails motherDetails, ContactDetails spouseDetails)
	{
		// Get the employee
		var employee = await GetEmployeeAsync(employeeId);

		// Update the family details
		employee.UpdateFamilyDetails(fatherDetails, motherDetails, spouseDetails);

		return employee;
	}

	public async Task<Employee> UpdateExperienceInfoByEmployeeIdAsync(Guid employeeId,
		List<WorkExperience> workExperiences)
	{
		// Get the employee
		var employee = await GetEmployeeAsync(employeeId);

		// Update the work experiences
		employee.UpdateWorkExperiences(workExperiences);

		return employee;
	}

	public async Task<Employee> UpdateBankDetailsByEmployeeIdAsync(Guid employeeId, BankDetails bankDetails)
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

			// Create and save the EmployeeDocument entity (metadata)
			var document = new EmployeeDocument(
				id: documentId,
				tenantId: CurrentTenant.Id,
				employeeId: employeeId,
				documentName: docData.FileName,
				contentType: docData.ContentType,
				size: docData.Size
				,
				blobName: blobName);

			await employeeDocumentRepository.InsertAsync(document);

			// Associate the document with the employee, if needed
			employee.AddDocument(document);
		}

		// Update the employee entity
		await employeeRepository.UpdateAsync(employee);
	}

	public async Task<EmployeeDocument> GetDocumentAsync(Guid employeeId, Guid documentId)
	{
		var document =
			await employeeDocumentRepository.FirstOrDefaultAsync(d => d.EmployeeId == employeeId && d.Id == documentId);
		return document ?? throw new BusinessException($"Document with ID {documentId} does not exist.");
	}

	public async Task<List<EmployeeDocument>> GetDocumentsAsync(Guid employeeId)
	{
		List<EmployeeDocument>
			documents = await employeeDocumentRepository.GetListAsync(d => d.EmployeeId == employeeId);
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

	private async Task<string> GenerateEmployeeCodeAsync()
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

		// Format the new EmployeeCode as "EMP" followed by a 3-digit number with leading zeros
		var newEmployeeCode = $"EMP{nextNumber:D3}";

		return newEmployeeCode;
	}
}