using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Common;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.Uow;

namespace ShikshaMitra.HR.Employees.PersonalInfo;

/// <summary>
/// Application service for managing employee personal information.
/// </summary>
[RemoteService(false)]
public class EmployeePersonalInfoAppService(
	IEmployeeRepository employeeRepository,
	EmployeeManager employeeManager,
	IdentityUserManager userManager,
	IIdentityUserRepository userRepository,
	ILookupNormalizer lookupNormalizer,
	IGuidGenerator guidGenerator,
	ILogger<EmployeePersonalInfoAppService> logger)
	: ApplicationService, IEmployeePersonalInfoAppService
{
	public async Task<EmployeePersonalInfoDto> CreateAsync(CreateEmployeePersonalInfoDto input)
	{
		// Validate required fields for user creation
		if (string.IsNullOrWhiteSpace(input.Email))
		{
			throw new UserFriendlyException("Email is required for employee creation.");
		}

		if (string.IsNullOrWhiteSpace(input.MobileNo))
		{
			throw new UserFriendlyException("Mobile number is required for employee creation.");
		}

		// Validate mobile number length
		if (input.MobileNo.Length < 4)
		{
			throw new UserFriendlyException("Mobile number must be at least 4 digits.");
		}

		// Check if email already exists
		var normalizedEmail = lookupNormalizer.NormalizeEmail(input.Email);
		var existingUserByEmail = await userRepository.FindByNormalizedEmailAsync(normalizedEmail);
		if (existingUserByEmail != null)
		{
			throw new UserFriendlyException($"Email '{input.Email}' is already in use.");
		}

		// Use explicit transaction to ensure atomicity
		using (var uow = UnitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true }))
		{
			try
			{
				// Step 1: Generate unique username
				var username = await GenerateUniqueUsernameAsync(input.FirstName, input.LastName);

				// Step 2: Generate password (username@last4digitsOfMobile)
				var last4Digits = input.MobileNo.Substring(input.MobileNo.Length - 4);
				var password = $"{username}@{last4Digits}";

				// Step 3: Create user
				var user = new IdentityUser(
					guidGenerator.Create(),
					username,
					input.Email,
					CurrentTenant.Id
				);

				user.Name = input.FirstName;
				user.Surname = input.LastName;
				user.SetEmailConfirmed(true);
				user.SetPhoneNumber(input.MobileNo, false);

				// Create user with password
				var userCreateResult = await userManager.CreateAsync(user, password);
				if (!userCreateResult.Succeeded)
				{
					throw new UserFriendlyException(
						"Failed to create user: " + 
						string.Join(", ", userCreateResult.Errors.Select(e => e.Description))
					);
				}

				// Verify user was created successfully
				var createdUser = await userRepository.FindAsync(user.Id);
				if (createdUser == null)
				{
					throw new BusinessException("User was not created successfully.");
				}

				// Step 4: Create employee using EmployeeManager with user's ID
				var employee = await employeeManager.CreateEmployeeAsync(
					userId: user.Id,
					roleId: input.RoleId,
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
					identificationMark1: input.IdentificationMark1,
					city: input.City,
					state: input.State,
					country: input.Country,
					pinCode: input.Pincode,
					shiftId: input.ShiftId
				);

					// Save the employee
					employee = await employeeRepository.InsertAsync(employee);

					logger.LogInformation(
						"[PersonalInfo] Employee created successfully: EmployeeId={EmployeeId}, EmployeeCode={EmployeeCode}",
						employee.Id,
						employee.EmployeeCode
					);

					// Commit transaction
					await uow.CompleteAsync();

					logger.LogInformation(
						"[PersonalInfo] Transaction completed: EmployeeId={EmployeeId}, Email={Email}",
						employee.Id,
						input.Email
					);

					// Return the mapped DTO
					return MapToDto(employee);
				}
				catch (UserFriendlyException ex)
				{
					logger.LogWarning(
						ex,
						"[PersonalInfo] Validation failed for {Email}: {Message}",
						input.Email,
						ex.Message
					);
					// Transaction will be automatically rolled back
					throw;
				}
				catch (Exception ex)
				{
					logger.LogError(
						ex,
						"[PersonalInfo] Unexpected error for {Email}",
						input.Email
					);
					// Transaction will be automatically rolled back
				throw;
			}
		}
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
						throw new UserFriendlyException("Unable to generate unique username.");
					}
				}
			}
			}
