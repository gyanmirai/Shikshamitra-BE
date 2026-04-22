using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Common;
using ShikshaMitra.HR;
using Volo.Abp;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;
using Volo.Abp.Identity;

namespace ShikshaMitra.HR.Employees;

public class EmployeeManager(
	IEmployeeRepository employeeRepository,
	IEmployeeFamilyMemberRepository familyMemberRepository,
	IIdentityUserRepository userRepository,
	IIdentityRoleRepository roleRepository,
	EmployeeCodeGenerator employeeCodeGenerator,
	UserManager<IdentityUser> userManager,
	ILogger<EmployeeManager> logger
) : DomainService
{
	/// <summary>Returns the employee or throws <see cref="BusinessException"/> when not found.</summary>
	/// <remarks><c>includeDetails: false</c> avoids the repository no-tracking "with details" query so callers that
	/// already loaded the same employee tracked in the same UoW do not get a second detached instance (EF update failure).</remarks>
	private async Task<Employee> GetEmployeeAsync(Guid employeeId)
	{
		return await employeeRepository.FindAsync(employeeId, includeDetails: false)
			?? throw new BusinessException(HRErrorCodes.EmployeeNotFound);
	}

	/// <summary>Returns the identity user or throws <see cref="BusinessException"/> when not found.</summary>
	private async Task<IdentityUser> GetUserAsync(Guid userId)
	{
		return await userRepository.FindAsync(userId)
			?? throw new BusinessException(HRErrorCodes.UserNotFound);
	}

	/// <summary>Returns the identity role or throws <see cref="BusinessException"/> when not found.</summary>
	private async Task<IdentityRole> GetRoleAsync(Guid roleId)
	{
		return await roleRepository.FindAsync(roleId)
			?? throw new BusinessException(HRErrorCodes.RoleNotFound);
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
		logger.LogDebug(
			"[EmployeeManager] Creating employee: UserId={UserId}, RoleId={RoleId}, Name={FirstName} {LastName}",
			userId,
			roleId,
			firstName,
			lastName
		);

		// Validate that the user exists
		var user = await userRepository.FindAsync(userId);
		if (user == null)
		{
			logger.LogError(
				"[EmployeeManager] User validation failed: UserId={UserId} does not exist",
				userId
			);
			throw new BusinessException(HRErrorCodes.UserNotFound);
		}

		logger.LogDebug("[EmployeeManager] User validated: UserId={UserId}", userId);

		// Validate role existence
		IdentityRole role = await GetRoleAsync(roleId);
		logger.LogDebug(
			"[EmployeeManager] Role validated: RoleId={RoleId}, RoleName={RoleName}",
			roleId,
			role.Name
		);

		var roleName = role.Name ?? throw new BusinessException(HRErrorCodes.RoleNameMissing);
		var isAlreadyInRole = await userManager.IsInRoleAsync(user, roleName);
		if (!isAlreadyInRole)
		{
			var addRoleResult = await userManager.AddToRoleAsync(user, roleName);
			if (!addRoleResult.Succeeded)
			{
				var errors = string.Join(", ", addRoleResult.Errors.Select(e => e.Description));
				logger.LogError(
					"[EmployeeManager] Failed to assign role '{RoleName}' to user {UserId}. Errors: {Errors}",
					roleName,
					userId,
					errors
				);
				throw new BusinessException(HRErrorCodes.RoleAssignmentFailed)
					.WithData("details", errors);
			}
		}

		// Generate employee code
		var employeeCode = await employeeCodeGenerator.GenerateAsync(
			dateOfJoining ?? DateOnly.FromDateTime(DateTime.Now),
			role
		);

		logger.LogInformation(
			"[EmployeeManager] Generated employee code: {EmployeeCode} for UserId={UserId}",
			employeeCode,
			userId
		);

		// Create employee entity with the provided userId as the employee ID
		var employee = new Employee(
			id: userId,
			tenantId: CurrentTenant.Id,
			userId: userId,
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

		logger.LogInformation(
			"[EmployeeManager] Employee entity created: EmployeeId={EmployeeId}, EmployeeCode={EmployeeCode}, UserId={UserId}, RoleId={RoleId}",
			employee.Id,
			employee.EmployeeCode,
			userId,
			roleId
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

		// Swap the user's role when it has changed
		if (roleId.HasValue && employee.RoleId != roleId.Value && employee.UserId != null)
		{
			IdentityUser user = await GetUserAsync(employee.UserId.Value);
			IdentityRole oldRole = await GetRoleAsync(employee.RoleId);
			IdentityRole newRole = await GetRoleAsync(roleId.Value);

			// Remove from current role — fail fast so the swap is atomic
			var removeResult = await userManager.RemoveFromRoleAsync(
				user,
				oldRole.Name ?? throw new BusinessException(HRErrorCodes.RoleNameMissing)
			);
			if (!removeResult.Succeeded)
			{
				throw new BusinessException(HRErrorCodes.RoleRemovalFailed);
			}

			// Assign to new role
			var addResult = await userManager.AddToRoleAsync(
				user,
				newRole.Name ?? throw new BusinessException(HRErrorCodes.RoleNameMissing)
			);
			if (!addResult.Succeeded)
			{
				throw new BusinessException(HRErrorCodes.RoleAssignmentFailed);
			}
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
		var today = DateOnly.FromDateTime(DateTime.UtcNow);

		// DateOfBirth is optional; only validate when provided
		if (employee.DateOfBirth.HasValue && employee.DateOfBirth.Value >= today)
		{
			throw new BusinessException(HRErrorCodes.DobInFuture);
		}

		// Both dates must be present for this cross-field rule to apply
		if (employee.DateOfJoining.HasValue && employee.DateOfBirth.HasValue
			&& employee.DateOfJoining.Value <= employee.DateOfBirth.Value)
		{
			throw new BusinessException(HRErrorCodes.DojBeforeDob);
		}
	}

	public async Task UpdateFamilyInfoByEmployeeIdAsync(
		Guid employeeId,
		ContactDetails? fatherDetails,
		ContactDetails? motherDetails,
		ContactDetails? spouseDetails
	)
	{
		// Guard: ensure the employee exists before touching related family records
		await GetEmployeeAsync(employeeId);

		// Update or create Father details
		await UpsertFamilyMemberAsync(employeeId, FamilyRelationType.Father, fatherDetails);

		// Update or create Mother details
		await UpsertFamilyMemberAsync(employeeId, FamilyRelationType.Mother, motherDetails);

		// Update or create Spouse details
		await UpsertFamilyMemberAsync(employeeId, FamilyRelationType.Spouse, spouseDetails);
	}

	private async Task UpsertFamilyMemberAsync(Guid employeeId, FamilyRelationType relationType, ContactDetails? details)
	{
		var existingMember = await familyMemberRepository.GetByEmployeeIdAndRelationTypeAsync(employeeId, relationType);

		if (details == null || string.IsNullOrWhiteSpace(details.Name))
		{
			// If details are empty and member exists, delete it
			if (existingMember != null)
			{
				await familyMemberRepository.DeleteAsync(existingMember);
			}
			return;
		}

		// Validate data before creating/updating
		ValidateFamilyMember(details, relationType);

		if (existingMember == null)
		{
			// Create new family member
			var newMember = new EmployeeFamilyMember(
				id: GuidGenerator.Create(),
				tenantId: CurrentTenant.Id,
				employeeId: employeeId,
				relationType: relationType,
				name: details.Name,
				occupation: details.Occupation,
				contactNo: details.ContactNo,
				address1: details.Address1,
				address2: details.Address2,
				address3: details.Address3,
				city: details.City,
				stateName: details.StateName,
				stateIsoCode: details.StateIsoCode,
				countryName: details.CountryName,
				countryIsoCode: details.CountryIsoCode,
				pinCode: details.PinCode
			);
			await familyMemberRepository.InsertAsync(newMember);
		}
		else
		{
			// Update existing family member
			existingMember.Update(
				name: details.Name,
				occupation: details.Occupation,
				contactNo: details.ContactNo,
				address1: details.Address1,
				address2: details.Address2,
				address3: details.Address3,
				city: details.City,
				stateName: details.StateName,
				stateIsoCode: details.StateIsoCode,
				countryName: details.CountryName,
				countryIsoCode: details.CountryIsoCode,
				pinCode: details.PinCode
			);
			await familyMemberRepository.UpdateAsync(existingMember);
		}
	}

	/// <summary>
	/// Validates family member data
	/// </summary>
	private static void ValidateFamilyMember(ContactDetails details, FamilyRelationType relationType)
	{
		if (string.IsNullOrWhiteSpace(details.Name))
		{
			throw new BusinessException(HRErrorCodes.FamilyMemberNameRequired);
		}

		if (details.Name.Length > 256)
		{
			throw new BusinessException(HRErrorCodes.FamilyMemberNameTooLong);
		}

		if (!string.IsNullOrWhiteSpace(details.ContactNo) && details.ContactNo.Length > 20)
		{
			throw new BusinessException(HRErrorCodes.FamilyContactTooLong);
		}

		if (!string.IsNullOrWhiteSpace(details.PinCode) && details.PinCode.Length > 10)
		{
			throw new BusinessException(HRErrorCodes.FamilyPincodeTooLong);
		}
	}

	/// <summary>
	/// Gets family members for an employee
	/// </summary>
	public async Task<List<EmployeeFamilyMember>> GetFamilyMembersByEmployeeIdAsync(Guid employeeId)
	{
		// Validate employee exists
		await GetEmployeeAsync(employeeId);

		return await familyMemberRepository.GetByEmployeeIdAsync(employeeId);
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
}
