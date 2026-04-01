using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Guids;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Identity;
using Volo.Abp.Uow;

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
	ILogger<EmployeePersonalInfoAppService> logger)
	: ApplicationService, IEmployeePersonalInfoAppService
{
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

		using var uow = UnitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true });

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

		var employee = await employeeManager.CreateEmployeeAsync(
			userId: user.Id, roleId: input.RoleId,
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

		if (profilePictureContent != null && !string.IsNullOrWhiteSpace(profilePictureFileName))
		{
			await employeeDocumentManager.UploadProfilePictureAsync(
				employee.Id, profilePictureFileName, profilePictureContent);
		}

		await uow.CompleteAsync();

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

		return MapToDto(employee);
	}

	public async Task<EmployeePersonalInfoDto> GetAsync(Guid id)
	{
		// Repository.GetAsync throws EntityNotFoundException if not found
		var employee = await employeeRepository.GetAsync(id);
		return MapToDto(employee);
	}

	public async Task<EmployeePersonalInfoDto> UpdateAsync(Guid id, UpdateEmployeePersonalInfoDto input)
	{
		// Update employee using EmployeeManager
		var employee = await employeeManager.UpdateEmployeeAsync(
			employeeId: id,
			firstName: input.FirstName,
			lastName: input.LastName,
			middleName: input.MiddleName,
			shortName: input.ShortName,
			employeeStatusType: input.EmployeeStatusType,
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

		// Return the mapped DTO
		return MapToDto(employee);
	}

	public async Task<EmployeeDto> GetByUserIdAsync(Guid userId)
	{
		var employee = await employeeRepository.GetEmployeeByUserIdAsync(userId)
			?? throw new EntityNotFoundException(typeof(Employee), userId);

		return ObjectMapper.Map<Employee, EmployeeDto>(employee);
	}

	public async Task<EmployeeDto> GetWithDetailsAsync(Guid id)
	{
		var employee = await employeeRepository.GetWithDetailsAsync(id, includePayrollStructure: true)
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
			IdentificationMark1 = employee.IdentificationMark1,
			Address1 = employee.Address1,
			Address2 = employee.Address2,
			Address3 = employee.Address3,
			Country = employee.Country,
			State = employee.State,
			City = employee.City,
			Pincode = employee.PinCode,
			ShiftId = employee.ShiftId,
			Shift = null // Navigation property not included in API contract
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
