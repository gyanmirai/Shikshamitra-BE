using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Common;
using ShikshaMitra.HR;
using ShikshaMitra.HR.Employees.Payroll;
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
using Volo.Abp.Identity;
using Volo.Abp.Uow;
using IdentityUser = Volo.Abp.Identity.IdentityUser;

namespace ShikshaMitra.HR.Employees;

[Obsolete("Use focused employee services instead. See docs/implementation/hr/application-layer-patterns.md")]
[RemoteService(false)]
public class EmployeeAppService(
	IEmployeeRepository employeeRepository,
	IEmployeeAdditionalDetailsRepository additionalDetailsRepository,
	IEmployeeFamilyMemberRepository familyMemberRepository,
	EmployeeManager employeeManager,
	EmployeeDocumentManager employeeDocumentManager,
	EmployeeAuthorizationService authorizationService,
	IBlobContainer blobContainer,
	IEmployeeCacheManager employeeCacheManager,
	IEmployeePayrollDetailsRepository payrollDetailsRepository,
	IEmployeePayrollComponentRepository payrollComponentRepository,
	IEmployeePayrollAppService employeePayrollAppService,
	IGuidGenerator guidGenerator,
	ILogger<EmployeeAppService> logger,
	IdentityUserManager userManager,
	IIdentityUserRepository userRepository,
	ILookupNormalizer lookupNormalizer,
	IEmployeeEmailService emailService,
	EmployeeOrganizationScopeResolver organizationScopeResolver) :
	CrudAppService<Employee, EmployeeDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateEmployeeDto>(
	employeeRepository),
	IEmployeeAppService
{
	public async Task<EmployeeDto> GetEmployeeByUserId(Guid userId)
	{
		// Navigation properties (Role, Shift) are auto-loaded
		Employee employee = await employeeRepository.GetEmployeeByUserIdAsync(userId)
			?? throw new EntityNotFoundException(typeof(Employee), userId);

		var dto = ObjectMapper.Map<Employee, EmployeeDto>(employee);
		await PopulateScopedOrganizationsAsync(dto, employee.UserId);
		return dto;
	}

	public override async Task<EmployeeDto> GetAsync(Guid id)
	{
		// Use GetWithDetailsAsync for complete employee data
		var employee = await employeeRepository.GetWithDetailsAsync(id)
			?? throw new EntityNotFoundException(typeof(Employee), id);

		var employeeDto = ObjectMapper.Map<Employee, EmployeeDto>(employee);
		await PopulateScopedOrganizationsAsync(employeeDto, employee.UserId);

		// Load family members from separate table
		await LoadFamilyMembersAsync(employeeDto, id);

		return employeeDto;
	}

	private async Task PopulateScopedOrganizationsAsync(EmployeeDto dto, Guid? userId)
	{
		var organizationsByUserId = await organizationScopeResolver.GetOrganizationsByUserIdsAsync([userId]);
		var organizations = userId.HasValue && organizationsByUserId.TryGetValue(userId.Value, out var scopedOrganizations)
			? scopedOrganizations
			: [];

		dto.OrganizationIds = organizations.Select(x => x.Id).ToList();
		dto.Organizations = organizations;
		dto.OrganizationNames = organizations
			.Select(x => x.Name)
			.Distinct()
			.OrderBy(x => x)
			.ToList();
	}

	private async Task LoadFamilyMembersAsync(EmployeeDto employeeDto, Guid employeeId)
	{
		var familyMembers = await familyMemberRepository.GetByEmployeeIdAsync(employeeId);

		var father = familyMembers.FirstOrDefault(fm => fm.RelationType == FamilyRelationType.Father);
		var mother = familyMembers.FirstOrDefault(fm => fm.RelationType == FamilyRelationType.Mother);
		var spouse = familyMembers.FirstOrDefault(fm => fm.RelationType == FamilyRelationType.Spouse);

		employeeDto.FatherDetails = MapFamilyMemberToContactDetailsDto(father);
		employeeDto.MotherDetails = MapFamilyMemberToContactDetailsDto(mother);
		employeeDto.SpouseDetails = MapFamilyMemberToContactDetailsDto(spouse);
	}

	private static ContactDetailsDto? MapFamilyMemberToContactDetailsDto(EmployeeFamilyMember? member)
	{
		if (member == null)
		{
			return null;
		}

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

	public async Task<EmployeeDto> CreateBasicInfoAsync(CreateEmployeeBasicInfoDto input)
	{
		logger.LogInformation(
			"Starting employee creation process for: {Email}, Name: {FirstName} {LastName}, Role: {RoleId}",
			input.Email,
			input.FirstName,
			input.LastName,
			input.RoleId
		);

		// Validate required fields for user creation
		if (string.IsNullOrWhiteSpace(input.Email))
		{
			logger.LogWarning("Employee creation failed: Email is required");
			throw new BusinessException(HRErrorCodes.EmployeeEmailRequired);
		}

		if (string.IsNullOrWhiteSpace(input.MobileNo))
		{
			logger.LogWarning("Employee creation failed: Mobile number is required for email: {Email}", input.Email);
			throw new BusinessException(HRErrorCodes.EmployeeMobileRequired);
		}

		// Validate mobile number length
		if (input.MobileNo.Length < 4)
		{
			logger.LogWarning(
				"Employee creation failed: Invalid mobile number length ({Length}) for email: {Email}",
				input.MobileNo.Length,
				input.Email
			);
			throw new BusinessException(HRErrorCodes.EmployeeMobileTooShort);
		}

		input.PanNo = NormalizePan(input.PanNo);
		input.AdharNo = NormalizeAdhar(input.AdharNo);
		await EnsurePanAndAdharAreUniqueAsync(input.PanNo, input.AdharNo);

		// Check if email already exists
		var normalizedEmail = lookupNormalizer.NormalizeEmail(input.Email);
		var existingUserByEmail = await userRepository.FindByNormalizedEmailAsync(normalizedEmail);
		if (existingUserByEmail != null)
		{
			logger.LogWarning(
				"Employee creation failed: Email '{Email}' is already in use by user {UserId}",
				input.Email,
				existingUserByEmail.Id
			);
			throw new BusinessException(HRErrorCodes.EmployeeEmailAlreadyExists)
				.WithData("email", input.Email);
		}

		// Use explicit transaction to ensure atomicity
		using (var uow = UnitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true }))
		{
			try
			{
				// Step 1: Generate unique username
				var username = await GenerateUniqueUsernameAsync(input.FirstName, input.LastName);
				logger.LogInformation(
					"Generated username '{Username}' for email: {Email}",
					username,
					input.Email
				);

				// Step 2: Generate password (Username@last4digitsOfMobile - with uppercase first letter)
				var last4Digits = input.MobileNo.Substring(input.MobileNo.Length - 4);
				var capitalizedUsername = char.ToUpper(username[0]) + username.Substring(1);
				var password = $"{capitalizedUsername}@{last4Digits}";

				// Step 3: Create user
				var userId = guidGenerator.Create();
				var user = new IdentityUser(
					userId,
					username,
					input.Email,
					CurrentTenant.Id
				);

				user.Name = input.FirstName;
				user.Surname = input.LastName;
				user.SetEmailConfirmed(true);
				user.SetPhoneNumber(input.MobileNo, false);

				logger.LogInformation(
					"Attempting to create user: UserId={UserId}, Username={Username}, Email={Email}, TenantId={TenantId}",
					userId,
					username,
					input.Email,
					CurrentTenant.Id
				);

				// Create user with password
				var userCreateResult = await userManager.CreateAsync(user, password);
				if (!userCreateResult.Succeeded)
				{
					var errors = string.Join(", ", userCreateResult.Errors.Select(e => e.Description));
					logger.LogError(
						"User creation failed for email: {Email}, Username: {Username}. Errors: {Errors}",
						input.Email,
						username,
						errors
					);
					throw new BusinessException(HRErrorCodes.EmployeeUserCreationFailed)
						.WithData("details", errors);
				}

				logger.LogInformation(
					"User created successfully: UserId={UserId}, Username={Username}, Email={Email}",
					userId,
					username,
					input.Email
				);

				// Verify user was created successfully
				var createdUser = await userRepository.FindAsync(user.Id);
				if (createdUser == null)
				{
					logger.LogError(
						"User verification failed: User with ID {UserId} was not found after creation",
						userId
					);
					throw new BusinessException(HRErrorCodes.EmployeeUserVerificationFailed);
				}

				logger.LogInformation(
					"User verified successfully: UserId={UserId}",
					userId
				);

				// Step 4: Create employee with the user's ID
				logger.LogInformation(
					"Creating employee record: UserId={UserId}, RoleId={RoleId}, Department={Department}, Designation={Designation}",
					user.Id,
					input.RoleId,
					input.Department,
					input.Designation
				);

				Employee employee = await employeeManager.CreateEmployeeAsync(
					userId: user.Id,
					roleId: input.RoleId,
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
					panNo: input.PanNo,
					adharNo: input.AdharNo,
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

				logger.LogInformation(
					"Employee record created successfully: EmployeeId={EmployeeId}, EmployeeCode={EmployeeCode}, UserId={UserId}",
					employee.Id,
					employee.EmployeeCode,
					user.Id
				);

				// Commit transaction
				await uow.CompleteAsync();

				logger.LogInformation(
					"Transaction completed successfully for employee creation: EmployeeId={EmployeeId}, EmployeeCode={EmployeeCode}, Email={Email}",
					employee.Id,
					employee.EmployeeCode,
					input.Email
				);

				// Send welcome email with credentials (fire and forget - email service handles exceptions)
				var fullName = $"{input.FirstName} {input.LastName}".Trim();

				await emailService.SendWelcomeEmailAsync(input.Email, fullName, username, password);

				logger.LogInformation(
					"Welcome email queued for employee: {Email}",
					input.Email
				);

				// Return the DTO
				return ObjectMapper.Map<Employee, EmployeeDto>(employee);
			}
			catch (BusinessException ex)
			{
				logger.LogWarning(
					ex,
					"Employee creation validation failed for email: {Email}. Message: {Message}",
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
					"Unexpected error during employee creation for email: {Email}, Name: {FirstName} {LastName}",
					input.Email,
					input.FirstName,
					input.LastName
				);
				// Transaction will be automatically rolled back
				throw;
			}
		}
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

	public async Task<EmployeeDto> UpdateBasicInfoAsync(Guid employeeId, UpdateEmployeeBasicInfoDto input)
	{
		input.PanNo = NormalizePan(input.PanNo);
		input.AdharNo = NormalizeAdhar(input.AdharNo);
		await EnsurePanAndAdharAreUniqueAsync(input.PanNo, input.AdharNo, employeeId);

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
			panNo: input.PanNo,
			adharNo: input.AdharNo,
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
			pinCode: input.PinCode,
			shiftId: input.ShiftId
		);

		// Save the updated employee (cache will be invalidated in repository)
		employee = await employeeRepository.UpdateAsync(employee);

		// Return the updated DTO
		return ObjectMapper.Map<Employee, EmployeeDto>(employee);
	}

	private async Task EnsurePanAndAdharAreUniqueAsync(string? panNo, string? adharNo, Guid? currentEmployeeId = null)
	{
		var query = await employeeRepository.GetQueryableAsync();

		if (!string.IsNullOrWhiteSpace(panNo))
		{
			var panExists = await AsyncExecuter.AnyAsync(
				query.Where(employee =>
					employee.TenantId == CurrentTenant.Id &&
					employee.PanNo == panNo &&
					(!currentEmployeeId.HasValue || employee.Id != currentEmployeeId.Value)));

			if (panExists)
			{
				throw new BusinessException(HRErrorCodes.EmployeePanAlreadyExists)
					.WithData("panNo", panNo);
			}
		}

		if (!string.IsNullOrWhiteSpace(adharNo))
		{
			var adharExists = await AsyncExecuter.AnyAsync(
				query.Where(employee =>
					employee.TenantId == CurrentTenant.Id &&
					employee.AdharNo == adharNo &&
					(!currentEmployeeId.HasValue || employee.Id != currentEmployeeId.Value)));

			if (adharExists)
			{
				throw new BusinessException(HRErrorCodes.EmployeeAdharAlreadyExists)
					.WithData("adharNo", adharNo);
			}
		}
	}

	private static string? NormalizePan(string? panNo) =>
		string.IsNullOrWhiteSpace(panNo) ? panNo : panNo.Trim().ToUpperInvariant();

	private static string? NormalizeAdhar(string? adharNo) =>
		string.IsNullOrWhiteSpace(adharNo) ? adharNo : adharNo.Trim();

	public async Task<EmployeeDto> UpdateFamilyInfo(Guid employeeId, UpdateEmployeeFamilyDetailsDto input)
	{
		// SECURITY: Check authorization before update
		await authorizationService.CheckEmployeeModifyAccessAsync(employeeId);

		// Convert the input to domain model and update the employee family members
		ContactDetails? fatherDetails = MapContactDetails(input.FatherDetails);
		ContactDetails? motherDetails = MapContactDetails(input.MotherDetails);
		ContactDetails? spouseDetails = MapContactDetails(input.SpouseDetails);

		await employeeManager.UpdateFamilyInfoByEmployeeIdAsync(employeeId, fatherDetails, motherDetails, spouseDetails);

		// Get employee to return (family members are now loaded separately)
		var employee = await employeeRepository.GetAsync(employeeId);

		// Invalidate cache
		await employeeCacheManager.InvalidateEmployeeAsync(employeeId);

		// Return the DTO
		return ObjectMapper.Map<Employee, EmployeeDto>(employee);
	}

	private ContactDetails? MapContactDetails(ContactDetailsDto? input)
	{
		return input == null
			? null
			: ObjectMapper.Map<ContactDetailsDto, ContactDetails>(input);
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
		await employeeDocumentManager.AddDocumentsAsync(employeeId, employeeDocuments);
	}

	public async Task<DocumentDto> GetDocumentAsync(Guid employeeId, Guid documentId)
	{
		EmployeeDocument document = await employeeDocumentManager.GetDocumentAsync(employeeId, documentId);

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
		var documents = await employeeDocumentManager.GetDocumentsAsync(employeeId);
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
		var updatedDocument = await employeeDocumentManager.UpdateDocumentAsync(
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
		string? changeReason, DateOnly? effectiveDate = null)
	{
		if (employeeStatusType != EmployeeStatusType.Active)
		{
			await EnsureNoActiveDirectReportsAsync(employeeId);
			effectiveDate ??= DateOnly.FromDateTime(Clock.Now.Date);
		}
		else
		{
			effectiveDate = null;
		}

		Employee employee =
			await employeeManager.UpdateEmployeeStatusAsync(employeeId, employeeStatusType, changeReason, effectiveDate);

		// Save the employee
		employee = await employeeRepository.UpdateAsync(employee);

		// Return the DTO
		return ObjectMapper.Map<Employee, EmployeeDto>(employee);
	}

	private async Task EnsureNoActiveDirectReportsAsync(Guid employeeId)
	{
		var employeeQuery = await employeeRepository.GetQueryableAsync();
		var activeDirectReportCount = await AsyncExecuter.CountAsync(
			employeeQuery.Where(e =>
				e.ReportingManagerId == employeeId &&
				e.EmployeeStatusType == EmployeeStatusType.Active));

		if (activeDirectReportCount > 0)
		{
			throw new BusinessException(HRErrorCodes.ReportingManagerHasActiveReports)
				.WithData("EmployeeId", employeeId)
				.WithData("ActiveDirectReportCount", activeDirectReportCount);
		}
	}


	public async Task DeleteDocumentAsync(Guid employeeId, Guid documentId)
	{
		await employeeDocumentManager.DeleteDocumentAsync(employeeId, documentId);
	}

	public async Task<string> UploadProfilePictureAsync(Guid employeeId, string fileName, byte[] fileContent)
	{
		return await employeeDocumentManager.UploadProfilePictureAsync(employeeId, fileName, fileContent);
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
				TotalWorkingHourPerMonth = payrollDetails.TotalWorkingHourPerMonth
			} : new PayrollDetailsDto(),
			Earnings = (earnings ?? Enumerable.Empty<EmployeePayrollComponent>()).Select(e => new PayrollComponentDto
			{
				Name = e.Name,
				Description = e.Description,
				Amount = e.Amount,
				IsFixed = e.IsFixed,
				IsTemp = e.IsTemp,
				ComponentType = e.ComponentType,
				DependOn = e.DependOn
			}).ToList(),
			Deductions = (deductions ?? Enumerable.Empty<EmployeePayrollComponent>()).Select(d => new PayrollComponentDto
			{
				Name = d.Name,
				Description = d.Description,
				Amount = d.Amount,
				IsFixed = d.IsFixed,
				IsTemp = d.IsTemp,
				ComponentType = d.ComponentType,
				DependOn = d.DependOn
			}).ToList()
		};

		return result;
	}

	public async Task UpdatePayrollDetailsAsync(Guid employeeId, UpdateEmployeePayrollDetailsDto input)
	{
		// Validate input
		if (input.PayrollDetails == null)
		{
			throw new BusinessException(HRErrorCodes.EmployeePayrollDetailsRequired);
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
				input.PayrollDetails.TotalWorkingHourPerMonth);
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
				input.PayrollDetails.TotalWorkingHourPerMonth);
			await payrollDetailsRepository.UpdateAsync(payrollDetails);
		}

		await payrollComponentRepository.DeleteByEmployeeIdAsync(employeeId, isEarning: true);
		await payrollComponentRepository.DeleteByEmployeeIdAsync(employeeId, isEarning: false);

		foreach (var earning in input.Earnings ?? Enumerable.Empty<PayrollComponentDto>())
		{
			var component = new EmployeePayrollComponent(
				guidGenerator.Create(),
				employeeId,
				earning.Name,
				componentId: null,
				earning.Description,
				earning.Amount,
				earning.IsFixed,
				earning.IsTemp,
				applicableMonth: null,
				earning.ComponentType,
				earning.DependOn,
				isEarning: true
			);
			await payrollComponentRepository.InsertAsync(component);
		}

		foreach (var deduction in input.Deductions ?? Enumerable.Empty<PayrollComponentDto>())
		{
			var component = new EmployeePayrollComponent(
				guidGenerator.Create(),
				employeeId,
				deduction.Name,
				componentId: null,
				deduction.Description,
				deduction.Amount,
				deduction.IsFixed,
				deduction.IsTemp,
				applicableMonth: null,
				deduction.ComponentType,
				deduction.DependOn,
				isEarning: false
			);
			await payrollComponentRepository.InsertAsync(component);
		}

		await employeeCacheManager.InvalidateEmployeeAsync(employeeId);
	}

	public async Task<EmployeeListResponse> GetFilteredListAsync(GetEmployeeListInput input)
	{
		var queryable = await Repository.GetQueryableAsync();

		// Apply filters
		if (!string.IsNullOrEmpty(input.Status))
		{
			if (Enum.TryParse<EmployeeStatusType>(input.Status, true, out var statusType))
			{
				queryable = queryable.Where(e => e.EmployeeStatusType == statusType);
			}
		}

		if (string.Equals(input.ReportingManagerStatus, "nonActive", StringComparison.OrdinalIgnoreCase))
		{
			queryable = queryable.Where(e =>
				e.EmployeeStatusType == EmployeeStatusType.Active &&
				e.ReportingManagerId.HasValue &&
				e.ReportingManager != null &&
				e.ReportingManager.EmployeeStatusType.HasValue &&
				e.ReportingManager.EmployeeStatusType.Value != EmployeeStatusType.Active);
		}

		if (input.IsTeaching.HasValue)
		{
			// IsTeaching is determined by EmployeeGroup in the domain model
			// Teaching = EmployeeGroupType values that represent teaching staff
			if (input.IsTeaching.Value)
			{
				queryable = queryable.Where(e => e.EmployeeGroup == EmployeeGroupType.Teaching);
			}
			else
			{
				queryable = queryable.Where(e => e.EmployeeGroup != EmployeeGroupType.Teaching);
			}
		}

		if (input.RoleId.HasValue)
		{
			queryable = queryable.Where(e => e.RoleId == input.RoleId.Value);
		}

		if (input.MediumStageId.HasValue)
		{
			var matchingUserIds = await organizationScopeResolver.GetUserIdsWithAnyStageAsync([input.MediumStageId.Value]);
			queryable = queryable.Where(e => e.UserId.HasValue && matchingUserIds.Contains(e.UserId.Value));
		}

		if (!string.IsNullOrWhiteSpace(input.Designation))
		{
			var designation = input.Designation.Trim().ToLower();
			queryable = queryable.Where(e => e.Designation != null && e.Designation.ToLower().Contains(designation));
		}

		queryable = ApplySearchFilter(queryable, input.Search);

		// Get total count before pagination
		var totalCount = await AsyncExecuter.CountAsync(queryable);

		// Apply sorting
		queryable = queryable.OrderBy(e => e.FirstName).ThenBy(e => e.LastName);

		// Apply pagination
		var employees = await AsyncExecuter.ToListAsync(
			queryable
				.Skip(input.SkipCount)
				.Take(input.MaxResultCount)
		);
		var organizationsByUserId = await organizationScopeResolver.GetOrganizationsByUserIdsAsync(
			employees.Select(x => x.UserId));

		// Calculate pagination info
		var pageSize = input.MaxResultCount > 0 ? input.MaxResultCount : 10;
		var currentPage = (input.SkipCount / pageSize) + 1;
		var totalPages = (int) Math.Ceiling((double) totalCount / pageSize);

		// Map to response DTOs
		var items = employees.Select(employee => MapToEmployeeSummary(employee, organizationsByUserId)).ToList();

		return new EmployeeListResponse
		{
			TotalCount = totalCount,
			CurrentPage = currentPage,
			PageSize = pageSize,
			TotalPages = totalPages,
			Items = items
		};
	}

	private static IQueryable<Employee> ApplySearchFilter(IQueryable<Employee> queryable, string? search)
	{
		if (string.IsNullOrWhiteSpace(search))
		{
			return queryable;
		}

		var searchTerm = search.Trim().ToLower();

		return queryable.Where(e =>
			(e.FirstName != null && e.FirstName.ToLower().Contains(searchTerm)) ||
			(e.LastName != null && e.LastName.ToLower().Contains(searchTerm)) ||
			(e.MiddleName != null && e.MiddleName.ToLower().Contains(searchTerm)) ||
			(e.EmployeeCode != null && e.EmployeeCode.ToLower().Contains(searchTerm)) ||
			(e.Email != null && e.Email.ToLower().Contains(searchTerm)) ||
			(e.MobileNo != null && e.MobileNo.Contains(searchTerm)) ||
			(e.Designation != null && e.Designation.ToLower().Contains(searchTerm))
		);
	}

	private EmployeeSummary MapToEmployeeSummary(
		Employee employee,
		IReadOnlyDictionary<Guid, List<EmployeeOrganizationDto>> organizationsByUserId)
	{
		var organizations = employee.UserId.HasValue &&
			organizationsByUserId.TryGetValue(employee.UserId.Value, out var scopedOrganizations)
				? scopedOrganizations
				: [];

		return new EmployeeSummary
		{
			Id = employee.Id,
			RoleName = employee.Role?.Name,
			FullName = employee.EmployeeName,
			ThumbnailPicture = employee.ProfilePictureBlobName,
			EmployeeIdentifier = employee.EmployeeCode,
			Dob = employee.DateOfBirth?.ToDateTime(TimeOnly.MinValue),
			Gender = employee.GenderType?.ToString(),
			Doj = employee.DateOfJoining?.ToDateTime(TimeOnly.MinValue),
			Email = employee.Email,
			MobileNo = employee.MobileNo,
			Department = employee.Department,
			Designation = employee.Designation,
			BloodGroup = employee.BloodGroup,
			Address = string.Join(", ", new[] { employee.Address1, employee.Address2, employee.Address3 }.Where(x => !string.IsNullOrWhiteSpace(x))),
			IsTeaching = employee.EmployeeGroup == EmployeeGroupType.Teaching,
			Shift = employee.Shift != null ? new ShiftInfo
			{
				Id = employee.Shift.Id,
				Name = employee.Shift.ShiftName ?? string.Empty,
				StartTime = employee.Shift.StartTime.ToString("HH:mm:ss"),
				EndTime = employee.Shift.EndTime.ToString("HH:mm:ss")
			} : null,
			Organizations = organizations,
			OrganizationNames = organizations
				.Select(x => x.Name)
				.Distinct()
				.OrderBy(x => x)
				.ToList(),
			Status = (int) (employee.EmployeeStatusType ?? EmployeeStatusType.Active)
		};
	}

	/// <summary>
	/// Gets the payroll details for an employee (matches OpenAPI Payroll contract).
	/// </summary>
	public Task<EmployeePayrollResponse> GetPayrollContractAsync(Guid employeeId) =>
		employeePayrollAppService.GetPayrollContractAsync(employeeId);

	/// <summary>
	/// Updates the payroll details for an employee (matches OpenAPI Payroll contract).
	/// </summary>
	public Task UpdatePayrollContractAsync(Guid employeeId, PayrollContract input) =>
		employeePayrollAppService.UpdatePayrollContractAsync(employeeId, input);
}
