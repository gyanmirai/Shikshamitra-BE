using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.TenantSettings.EducationBoards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using IdentityUser = Volo.Abp.Identity.IdentityUser;

namespace ShikshaMitra.HR.Employees.PersonalInfo;

/// <summary>
/// Single owner of employee lifecycle: create, read, update basic info.
/// </summary>
[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View)]
public class EmployeePersonalInfoAppService(
	IEmployeeRepository employeeRepository,
	IEmployeeFamilyMemberRepository familyMemberRepository,
	EmployeeManager employeeManager,
	EmployeeDocumentManager employeeDocumentManager,
	IdentityUserManager userManager,
	IIdentityUserRepository userRepository,
	ILookupNormalizer lookupNormalizer,
	IGuidGenerator guidGenerator,
	IEmployeeEmailService emailService,
	IDistributedEventBus distributedEventBus,
	IHrPolicyAssignmentAppService policyAssignmentAppService,
	IRepository<EmployeeOrganizationAssignment, Guid> employeeOrganizationAssignmentRepository,
	IRepository<EducationMediumStage, Guid> educationMediumStageRepository,
	IDataFilter<IMultiTenant> multiTenantDataFilter,
	ILogger<EmployeePersonalInfoAppService> logger)
	: ApplicationService, IEmployeePersonalInfoAppService
{
	private const int MaxEmployeeCodeInsertAttempts = 5;

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add)]
	public async Task<EmployeePersonalInfoDto> CreateAsync(CreateEmployeePersonalInfoDto input)
	{
		return await CreateCoreAsync(input, null, null);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add)]
	public async Task<EmployeePersonalInfoDto> CreateWithProfilePictureAsync(
		CreateEmployeePersonalInfoDto input,
		string profilePictureFileName,
		byte[] profilePictureContent)
	{
		return await CreateCoreAsync(input, profilePictureFileName, profilePictureContent);
	}

	private async Task<EmployeePersonalInfoDto> CreateCoreAsync(
		CreateEmployeePersonalInfoDto input,
		string? profilePictureFileName,
		byte[]? profilePictureContent)
	{
		var organizationIds = await ValidateOrganizationIdsAsync(input.OrganizationIds);

		if (string.IsNullOrWhiteSpace(input.Email))
			throw new BusinessException(HRErrorCodes.EmployeeEmailRequired);

		if (string.IsNullOrWhiteSpace(input.MobileNo))
			throw new BusinessException(HRErrorCodes.EmployeeMobileRequired);

		if (input.MobileNo.Length < 4)
			throw new BusinessException(HRErrorCodes.EmployeeMobileTooShort);

		var normalizedEmail = lookupNormalizer.NormalizeEmail(input.Email);
		var existingUserByEmail = await userRepository.FindByNormalizedEmailAsync(normalizedEmail);
		if (existingUserByEmail != null)
			throw new BusinessException(HRErrorCodes.EmployeeEmailAlreadyExists);

		var username = await GenerateUniqueUsernameAsync(input.FirstName, input.LastName);
		var last4Digits = input.MobileNo[^4..];
		var capitalizedUsername = char.ToUpper(username[0]) + username[1..];
		var password = $"{capitalizedUsername}@{last4Digits}";

		var user = new IdentityUser(guidGenerator.Create(), username, input.Email, CurrentTenant.Id);
		user.Name = input.FirstName;
		user.Surname = input.LastName;
		user.SetEmailConfirmed(true);
		user.SetPhoneNumber(input.MobileNo, false);

		var userCreateResult = await userManager.CreateAsync(user, password);
		if (!userCreateResult.Succeeded)
		{
			var errors = string.Join(", ", userCreateResult.Errors.Select(e => e.Description));
			logger.LogError("User creation failed for {Email}: {Errors}", input.Email, errors);
			throw new BusinessException(HRErrorCodes.EmployeeUserCreationFailed).WithData("details", errors);
		}

		Employee employee;
		try
		{
			employee = await CreateAndInsertEmployeeWithRetryAsync(user.Id, input, organizationIds);
		}
		catch
		{
			var deleteResult = await userManager.DeleteAsync(user);
			if (!deleteResult.Succeeded)
			{
				var deleteErrors = string.Join(", ", deleteResult.Errors.Select(e => e.Description));
				logger.LogWarning(
					"Employee creation failed and cleanup user delete also failed for {Email}: {Errors}",
					input.Email,
					deleteErrors);
			}

			throw;
		}

		if (profilePictureContent != null && !string.IsNullOrWhiteSpace(profilePictureFileName))
		{
			await employeeDocumentManager.UploadProfilePictureAsync(
				employee.Id, profilePictureFileName, profilePictureContent);
		}

		logger.LogInformation(
			"Employee created: EmployeeId={EmployeeId}, Code={EmployeeCode}, Email={Email}",
			employee.Id, employee.EmployeeCode, input.Email);

		await distributedEventBus.PublishAsync(new HR.Events.EmployeeCreatedEto
		{
			EmployeeId = employee.Id,
			EmployeeCode = employee.EmployeeCode,
			FullName = $"{input.FirstName} {input.LastName}".Trim(),
			Email = input.Email,
			TenantId = CurrentTenant.Id,
			CreatedAt = DateTime.UtcNow
		});

		var fullName = $"{input.FirstName} {input.LastName}".Trim();
		await emailService.SendWelcomeEmailAsync(input.Email, fullName, username, password);

		return MapToDto(await employeeRepository.GetAsync(employee.Id));
	}

	private async Task<Employee> CreateAndInsertEmployeeWithRetryAsync(
		Guid userId,
		CreateEmployeePersonalInfoDto input,
		IReadOnlyCollection<Guid> organizationIds)
	{
		for (var attempt = 1; attempt <= MaxEmployeeCodeInsertAttempts; attempt++)
		{
			try
			{
				using var uow = UnitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true });

				var employee = await employeeManager.CreateEmployeeAsync(
					userId: userId, roleId: input.RoleId,
					firstName: input.FirstName, lastName: input.LastName,
					middleName: input.MiddleName, shortName: input.ShortName,
					employeeStatusType: input.EmployeeStatusType, dateOfBirth: input.DateOfBirth,
					familyCast: input.FamilyCaste, castType: input.CastType,
					genderType: input.GenderType, religion: input.Religion,
					maritalStatus: input.MaritalStatus, bloodGroup: input.BloodGroup,
					mobileNo: input.MobileNo, secondaryMobileNo: input.SecondaryMobileNo,
					email: input.Email, address1: input.Address1, address2: input.Address2,
					address3: input.Address3, department: input.Department,
					designation: input.Designation, dateOfJoining: input.DateOfJoining,
					employeeGroup: input.EmployeeGroup, qualification: input.Qualification,
					identificationMark1: input.IdentificationMark1, city: input.City,
					state: input.State, country: input.Country, pinCode: input.Pincode,
					shiftId: input.ShiftId);

				employee = await employeeRepository.InsertAsync(employee, autoSave: true);
				await ReplaceOrganizationAssignmentsAsync(employee.Id, organizationIds);
				if (input.PolicyOverride != null)
				{
					await CreateDirectPolicyOverrideAsync(employee.Id, input.PolicyOverride);
				}
				await uow.CompleteAsync();
				return employee;
			}
			catch (Exception ex) when (IsEmployeeCodeUniqueConstraintViolation(ex) && attempt < MaxEmployeeCodeInsertAttempts)
			{
				logger.LogWarning(
					ex,
					"Employee code collision detected for {Email}. Retrying create flow (attempt {Attempt}/{MaxAttempts}).",
					input.Email,
					attempt + 1,
					MaxEmployeeCodeInsertAttempts);
			}
		}

		throw new InvalidOperationException("Employee creation retry loop exited unexpectedly.");
	}

	private async Task CreateDirectPolicyOverrideAsync(Guid employeeId, CreateEmployeePolicyOverrideDto input)
	{
		await policyAssignmentAppService.UpsertEmployeeOverrideAsync(new UpsertEmployeePolicyOverrideDto
		{
			EmployeeId = employeeId,
			AcademicYearId = input.AcademicYearId,
			AttendancePolicyId = input.AttendancePolicyId,
			PayrollPolicyId = input.PayrollPolicyId,
			EffectiveFrom = input.EffectiveFrom,
			EffectiveTo = input.EffectiveTo,
			AssignmentReason = input.AssignmentReason ?? "Employee creation override",
			ConfirmationToken = input.ConfirmationToken
		});
	}

	private async Task<List<Guid>> ValidateOrganizationIdsAsync(IEnumerable<Guid>? organizationIds)
	{
		var distinctIds = (organizationIds ?? [])
			.Where(id => id != Guid.Empty)
			.Distinct()
			.ToList();
		if (distinctIds.Count == 0)
		{
			throw new BusinessException(HRErrorCodes.EmployeeOrganizationRequired);
		}

		using var _ = CurrentTenant.Id == null ? multiTenantDataFilter.Disable() : null;
		var query = await educationMediumStageRepository.GetQueryableAsync();
		var existingIds = await AsyncExecuter.ToListAsync(
			query.Where(x => distinctIds.Contains(x.Id)).Select(x => x.Id));

		if (existingIds.Count != distinctIds.Count)
		{
			throw new BusinessException(HRErrorCodes.EmployeeOrganizationInvalid);
		}

		return distinctIds;
	}

	private async Task ReplaceOrganizationAssignmentsAsync(
		Guid employeeId,
		IReadOnlyCollection<Guid> organizationIds)
	{
		var existing = await employeeOrganizationAssignmentRepository.GetListAsync(x => x.EmployeeId == employeeId);
		if (existing.Count > 0)
		{
			await employeeOrganizationAssignmentRepository.DeleteManyAsync(existing, autoSave: false);
		}

		var assignments = organizationIds
			.Select(id => new EmployeeOrganizationAssignment(
				guidGenerator.Create(),
				CurrentTenant.Id,
				employeeId,
				id))
			.ToList();

		await employeeOrganizationAssignmentRepository.InsertManyAsync(assignments, autoSave: false);
	}

	private static bool IsEmployeeCodeUniqueConstraintViolation(Exception exception)
	{
		for (var current = exception; current != null; current = current.InnerException)
		{
			var message = current.Message;
			if (
				message.Contains("IX_Employees_TenantId_EmployeeCode_Unique", StringComparison.OrdinalIgnoreCase) ||
				message.Contains("AppEmployees.TenantId, AppEmployees.EmployeeCode", StringComparison.OrdinalIgnoreCase) ||
				message.Contains("duplicate key value violates unique constraint", StringComparison.OrdinalIgnoreCase) ||
				message.Contains("Cannot insert duplicate key row", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return false;
	}

	public async Task<EmployeePersonalInfoDto> GetAsync(Guid id)
	{
		// Repository.GetAsync throws EntityNotFoundException if not found
		var employee = await employeeRepository.GetAsync(id);
		return MapToDto(employee);
	}

	public async Task<EmployeePersonalInfoDto> UpdateAsync(Guid id, UpdateEmployeePersonalInfoDto input)
	{
		var organizationIds = await ValidateOrganizationIdsAsync(input.OrganizationIds);
		var existingEmployee = await employeeRepository.GetAsync(id);

		// Update employee using EmployeeManager
		var employee = await employeeManager.UpdateEmployeeAsync(
			employeeId: id,
			firstName: input.FirstName,
			lastName: input.LastName,
			middleName: input.MiddleName,
			shortName: input.ShortName,
			employeeStatusType: existingEmployee.EmployeeStatusType ?? EmployeeStatusType.Active,
			dateOfBirth: input.DateOfBirth,
			familyCast: input.FamilyCaste, // DTO uses correct spelling, entity uses FamilyCast
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
			identificationMark1: input.IdentificationMark1,
			city: input.City,
			state: input.State,
			country: input.Country,
			pinCode: input.Pincode,
			shiftId: input.ShiftId
		);

		// Save the updated employee
		employee = await employeeRepository.UpdateAsync(employee);
		await ReplaceOrganizationAssignmentsAsync(employee.Id, organizationIds);

		// Return the mapped DTO
		return MapToDto(await employeeRepository.GetAsync(employee.Id));
	}

	public async Task<EmployeeDto> GetByUserIdAsync(Guid userId)
	{
		var employee = await employeeRepository.GetEmployeeByUserIdAsync(userId)
			?? throw new EntityNotFoundException(typeof(Employee), userId);

		return ObjectMapper.Map<Employee, EmployeeDto>(employee);
	}

	public async Task<EmployeeDto> GetWithDetailsAsync(Guid id)
	{
		var employee = await employeeRepository.GetWithDetailsAsync(id)
			?? throw new EntityNotFoundException(typeof(Employee), id);

		var employeeDto = ObjectMapper.Map<Employee, EmployeeDto>(employee);

		var familyMembers = await familyMemberRepository.GetByEmployeeIdAsync(id);
		var father = familyMembers.FirstOrDefault(fm => fm.RelationType == FamilyRelationType.Father);
		var mother = familyMembers.FirstOrDefault(fm => fm.RelationType == FamilyRelationType.Mother);
		var spouse = familyMembers.FirstOrDefault(fm => fm.RelationType == FamilyRelationType.Spouse);

		employeeDto.FatherDetails = MapFamilyMemberToContactDetailsDto(father);
		employeeDto.MotherDetails = MapFamilyMemberToContactDetailsDto(mother);
		employeeDto.SpouseDetails = MapFamilyMemberToContactDetailsDto(spouse);

		return employeeDto;
	}

	private static ContactDetailsDto? MapFamilyMemberToContactDetailsDto(EmployeeFamilyMember? member)
	{
		if (member == null) return null;

		return new ContactDetailsDto
		{
			Name = member.Name,
			Occupation = member.Occupation,
			ContactNo = member.ContactNo,
			Address1 = member.Address1,
			Address2 = member.Address2,
			Address3 = member.Address3,
			City = member.City,
			StateName = member.StateName,
			StateIsoCode = member.StateIsoCode,
			CountryName = member.CountryName,
			CountryIsoCode = member.CountryIsoCode,
			PinCode = member.PinCode
		};
	}

	public async Task<EmployeeDto> UpdateBankDetailsAsync(Guid employeeId, BankDetailsDto input)
	{
		var bankDetails = ObjectMapper.Map<BankDetailsDto, BankDetails>(input);
		var employee = await employeeManager.UpdateBankDetailsByEmployeeIdAsync(employeeId, bankDetails);
		employee = await employeeRepository.UpdateAsync(employee);
		return ObjectMapper.Map<Employee, EmployeeDto>(employee);
	}

	private EmployeePersonalInfoDto MapToDto(Employee employee)
	{
		return new EmployeePersonalInfoDto
		{
			Id = employee.Id,
			EmployeeCode = employee.EmployeeCode,
			FullName = employee.EmployeeName,
			UserId = employee.UserId,
			FirstName = employee.FirstName ?? string.Empty,
			LastName = employee.LastName ?? string.Empty,
			MiddleName = employee.MiddleName,
			ShortName = employee.ShortName,
			EmployeeStatusType = employee.EmployeeStatusType ?? EmployeeStatusType.Active,
			DateOfBirth = employee.DateOfBirth,
			FamilyCaste = employee.FamilyCast, // Entity uses FamilyCast
			CastType = employee.CastType,
			GenderType = employee.GenderType,
			Religion = employee.Religion,
			MaritalStatus = employee.MaritalStatus,
			BloodGroup = employee.BloodGroup,
			MobileNo = employee.MobileNo ?? string.Empty,
			SecondaryMobileNo = employee.SecondaryMobileNo,
			Email = employee.Email,
			Department = employee.Department,
			Designation = employee.Designation,
			DateOfJoining = employee.DateOfJoining,
			EmployeeGroup = employee.EmployeeGroup,
			Qualification = employee.Qualification,
			RoleId = employee.RoleId,
			RoleName = employee.Role?.Name,
			ProfilePictureBlobName = employee.ProfilePictureBlobName,
			IdentificationMark1 = employee.IdentificationMark1,
			Address1 = employee.Address1,
			Address2 = employee.Address2,
			Address3 = employee.Address3,
			Country = employee.Country,
			State = employee.State,
			City = employee.City,
			Pincode = employee.PinCode,
			ShiftId = employee.ShiftId,
			Shift = null, // Navigation property not included in API contract
			OrganizationIds = employee.OrganizationAssignments
				.Select(x => x.EducationMediumStageId)
				.Distinct()
				.ToList(),
			Organizations = employee.OrganizationAssignments
				.Where(x => x.EducationMediumStage != null)
				.Select(x => new EmployeeOrganizationDto
				{
					Id = x.EducationMediumStageId,
					Name = x.EducationMediumStage!.Name
				})
				.OrderBy(x => x.Name)
				.ToList(),
			OrganizationNames = employee.OrganizationAssignments
				.Where(x => x.EducationMediumStage != null)
				.Select(x => x.EducationMediumStage!.Name)
				.Distinct()
				.OrderBy(x => x)
				.ToList()
		};
	}

	private async Task<string> GenerateUniqueUsernameAsync(string firstName, string lastName)
	{
		// Generate base username: firstInitial + lastName (e.g., jdoe)
		// Sanitize to remove special characters and non-ASCII characters
		var firstInitial = !string.IsNullOrWhiteSpace(firstName)
			? new string(firstName.Where(char.IsLetterOrDigit).ToArray()).ToLower().FirstOrDefault().ToString()
			: "x";

		if (string.IsNullOrWhiteSpace(firstInitial))
		{
			firstInitial = "x";
		}

		var lastNamePart = !string.IsNullOrWhiteSpace(lastName)
			? new string(lastName.Where(char.IsLetterOrDigit).ToArray()).ToLower()
			: "user";

		if (string.IsNullOrWhiteSpace(lastNamePart))
		{
			lastNamePart = "user";
		}

		var baseUsername = $"{firstInitial}{lastNamePart}";

		// Check if username exists
		var normalizedUsername = lookupNormalizer.NormalizeName(baseUsername);
		var existingUser = await userRepository.FindByNormalizedUserNameAsync(normalizedUsername);

		if (existingUser == null)
		{
			return baseUsername;
		}

		// Username exists, append sequence number
		var sequence = 1;
		while (true)
		{
			var sequencedUsername = $"{baseUsername}{sequence:D2}";
			normalizedUsername = lookupNormalizer.NormalizeName(sequencedUsername);
			existingUser = await userRepository.FindByNormalizedUserNameAsync(normalizedUsername);

			if (existingUser == null)
			{
				return sequencedUsername;
			}

			sequence++;
			if (sequence > 99)
			{
				throw new BusinessException(HRErrorCodes.EmployeeUsernameGenerationFailed);
			}
		}
	}
}
