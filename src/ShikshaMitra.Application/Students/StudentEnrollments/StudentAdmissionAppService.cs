using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Compliance;
using ShikshaMitra.Students.Admissions;
using ShikshaMitra.Students.Enrollments.Admissions;
using ShikshaMitra.Students.Enrollments.Registrations;
using ShikshaMitra.Students.StudentEnrollments.Registration;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Guids;
using Volo.Abp.Domain.Entities;
using ShikshaMitra.Students.ValueObjects;
using ShikshaMitra.Students.StudentEnrollments.Managers;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.StudentRecords;
using ShikshaMitra.Library.Members;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;

namespace ShikshaMitra.Students.StudentEnrollments;

/// <summary>
/// Application service for Student Admission operations
/// </summary>
[Authorize]
[RemoteService(false)]
public class StudentAdmissionAppService(
	IStudentRepository studentRepository,
	IRepository<Division, Guid> divisionRepository,
	IRepository<Standard, Guid> standardRepository,
	IdentityUserManager userManager,
	IGuidGenerator guidGenerator,
	IStudentEmailService studentEmailService,
	AdmissionManager admissionManager,
	EntityValidationService validationService,
	IUserScopeAccessService userScopeAccessService,
	StudentScopeAccessService studentScopeAccessService,
	StudentIdentityComplianceAppService identityComplianceAppService,
	ILibraryMemberAppService libraryMemberAppService) : ApplicationService, IStudentAdmissionAppService
{
	/// <summary>
	/// Gets a paginated list of students in admission stage with filters
	/// </summary>
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_Admission.View)]
	public async Task<PagedResultDto<StudentDto>> GetListAsync(GetStudentAdmissionsInput input)
	{
		var query = await studentRepository.GetQueryableWithoutAutoIncludesAsync();

		if (input.ApprovalStatus == StudentEnrollmentApprovalState.Approved)
		{
			query = query.Where(s => s.Status == StudentLifeCycleStatusEnum.Enrolled && s.AdmissionApprovalStatus == StudentEnrollmentApprovalState.Approved);
		}
		else if (input.ApprovalStatus == StudentEnrollmentApprovalState.Rejected)
		{
			query = query.Where(s => s.Status == StudentLifeCycleStatusEnum.Admission && s.AdmissionApprovalStatus == StudentEnrollmentApprovalState.Rejected);
		}
		else
		{
			query = query.Where(s => s.Status == StudentLifeCycleStatusEnum.Admission && s.AdmissionApprovalStatus == StudentEnrollmentApprovalState.Pending);
		}

		query = await studentScopeAccessService.ApplyCurrentUserScopeAsync(query);

		if (input.AcademicYearId.HasValue && input.AcademicYearId.Value != Guid.Empty)
		{
			query = query.Where(s => s.AcademicYearId == input.AcademicYearId.Value);
		}

		if (input.AcademicMediumId.HasValue && input.AcademicMediumId.Value != Guid.Empty)
		{
			query = query.Where(s => s.AcademicMediumId == input.AcademicMediumId.Value);
		}

		if (input.EducationMediumStageId.HasValue && input.EducationMediumStageId.Value != Guid.Empty)
		{
			query = query.Where(s => s.AcademicMediumStageId == input.EducationMediumStageId.Value);
		}

		if (input.StandardId.HasValue && input.StandardId.Value != Guid.Empty)
		{
			query = query.Where(s => s.StandardId == input.StandardId.Value);
		}

		if (input.DivisionId.HasValue && input.DivisionId.Value != Guid.Empty)
		{
			query = query.Where(s => s.DivisionId == input.DivisionId.Value);
		}

		// Apply filters
		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			query = query.Where(s =>
				(s.FirstName != null && s.FirstName.Contains(input.Search)) ||
				(s.MiddleName != null && s.MiddleName.Contains(input.Search)) ||
				(s.LastName != null && s.LastName.Contains(input.Search)) ||
				(s.GrNo != null && s.GrNo.ToString().Contains(input.Search)));
		}

		query = query.Where(s => s.AdmissionApprovalStatus == input.ApprovalStatus);

		// Get total count
		var totalCount = await studentRepository.AsyncExecuter.CountAsync(query);

		var registrationProgressDenominator = Math.Max(1, Enum.GetValues<StudentRegistrationStateEnum>().Length - 1);

		// Apply sorting, paging, and a direct projection to avoid materializing the full student aggregate graph.
		var pageQuery = query.OrderByDescending(s => s.CreationTime)
			.PageBy(input.SkipCount, input.MaxResultCount);

		var studentDtos = await AsyncExecuter.ToListAsync(pageQuery.Select(s => new StudentDto
		{
			StudentId = s.Id,
			InquiryNo = s.AdditionalDetail != null ? s.AdditionalDetail.InquiryNo : string.Empty,
			FormNo = s.AdditionalDetail != null ? s.AdditionalDetail.RegistrationFormNo : null,
			StudentName = ((s.FirstName ?? string.Empty) + " " + (s.MiddleName ?? string.Empty) + " " + (s.LastName ?? string.Empty)).Trim(),
			FirstName = s.FirstName,
			MiddleName = s.MiddleName,
			LastName = s.LastName,
			AcademicYearId = s.AcademicYearId,
			AcademicYearName = s.AcademicYear != null ? s.AcademicYear.Name : null,
			AcademicBoardId = s.AcademicBoardId,
			AcademicBoardName = s.AcademicBoard == null
				? null
				: s.AcademicBoard.Type == EducationBoardType.CBSE
					? "CBSE"
					: "GSEB",
			AcademicMediumId = s.AcademicMediumId,
			AcademicMediumName = s.AcademicMedium != null ? s.AcademicMedium.Name : null,
			StandardId = s.StandardId ?? Guid.Empty,
			Standard = s.Standard != null ? s.Standard.Name : string.Empty,
			DivisionId = s.DivisionId,
			Division = s.Division != null ? s.Division.Name : null,
			DateOfBirth = s.AdditionalDetail != null ? s.AdditionalDetail.DateOfBirth : null,
			DateOfInquiry = s.AdditionalDetail != null ? s.AdditionalDetail.DateOfInquiry : null,
			Gender = s.AdditionalDetail != null ? s.AdditionalDetail.Gender : null,
			RollNo = s.RollNo,
			GrNo = s.GrNo,
			Category = s.AdditionalDetail != null ? s.AdditionalDetail.Category : Category.General,
			IsLibraryMember = s.AdditionalDetail != null && s.AdditionalDetail.IsLibraryMember == true,
			IsTransportMember = s.AdditionalDetail != null && s.AdditionalDetail.IsTransportMember == true,
			Address1 = s.ContactDetail != null ? s.ContactDetail.Address1 : null,
			Address2 = s.ContactDetail != null ? s.ContactDetail.Address2 : null,
			Address3 = s.ContactDetail != null ? s.ContactDetail.Address3 : null,
			City = s.ContactDetail != null ? s.ContactDetail.City : null,
			State = s.ContactDetail != null ? s.ContactDetail.State : null,
			Country = s.ContactDetail != null ? s.ContactDetail.Country : null,
			PinCode = s.ContactDetail != null ? s.ContactDetail.Pincode : null,
			PrimaryContactNo = s.ContactDetail != null ? s.ContactDetail.PrimaryContactNo : null,
			SecondaryContactNo = s.ContactDetail != null ? s.ContactDetail.SecondaryContactNo : null,
			Email = s.ContactDetail != null ? s.ContactDetail.Email : null,
			PreviousSchool = s.AdditionalDetail != null ? s.AdditionalDetail.PreviousSchool : null,
			InquiryPreferenceId = s.AdditionalDetail != null ? s.AdditionalDetail.InquiryPreferenceId : null,
			InquiryPreference = s.AdditionalDetail != null && s.AdditionalDetail.InquiryPreference != null
				? s.AdditionalDetail.InquiryPreference.PreferenceName
				: null,
			NextReminderDate = s.Notes
				.OrderByDescending(note => note.CreationTime)
				.Select(note => note.NextReminderDate)
				.FirstOrDefault(),
			InquiryStatus = s.Notes
				.OrderByDescending(note => note.CreationTime)
				.Select(note => note.Status)
				.FirstOrDefault(),
			Progress = s.AdditionalDetail != null
				? ((double)(int)s.AdditionalDetail.StudentRegistrationState / registrationProgressDenominator) * 100
				: 0,
			Remarks = s.AdmissionApprovalRemarks,
			IsFeeStructureAssigned = s.FeeStructureId.HasValue,
			HasPhoto = !string.IsNullOrWhiteSpace(s.PhotoBlobName),
			PhotoUrl = s.PhotoBlobName
		}));

		foreach (var dto in studentDtos)
		{
			dto.Address = string.Join(", ", new[]
			{
				dto.Address1,
				dto.Address2,
				dto.Address3,
				dto.City,
				dto.State?.Name,
				dto.Country?.Name
			}.Where(x => !string.IsNullOrWhiteSpace(x)));
		}

		return new PagedResultDto<StudentDto>(totalCount, studentDtos);
	}

	/// <summary>
	/// Assigns or changes division for a student
	/// </summary>
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_Admission.Add)]
	public async Task AssignDivisionAsync(Guid studentId, AssignDivisionDto input)
	{
		await admissionManager.CanAssignDivisionAsync(studentId, input.DivisionId);

		// Get student
		var student = await studentRepository.GetAsync(studentId) ?? throw new Exception($"Student with ID {studentId} does not exist.");
		await userScopeAccessService.EnsureCurrentUserCanAccessAsync(
			student,
			operation: nameof(AssignDivisionAsync));
		await studentScopeAccessService.EnsureCanAccessDivisionAsync(
			input.DivisionId,
			nameof(AssignDivisionAsync));

		// Assign division using domain method
		student.AssignDivision(input.DivisionId);

		// Save changes
		await studentRepository.UpdateAsync(student);
	}

	/// <summary>
	/// Updates general details for a student in admission stage.
	/// </summary>
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_Admission.Add)]
	public async Task<ShikshaMitra.Students.Enrollments.Registrations.StudentGeneralDetailsDto> UpdateAdmissionGeneralDetailsAsync(
		Guid studentId,
		ShikshaMitra.Students.Enrollments.Registrations.UpdateGeneralInfoDto input)
	{
		var student = await studentRepository.GetRegistrationAggregateAsync(studentId)
			?? throw new EntityNotFoundException(typeof(Student), studentId);

		await userScopeAccessService.EnsureCurrentUserCanAccessAsync(
			student,
			operation: nameof(UpdateAdmissionGeneralDetailsAsync));

		if (student.Status != StudentLifeCycleStatusEnum.Admission)
		{
			throw new BusinessException(
					StudentRegistrationErrorCodes.InvalidStudentStatusForGeneralInfo,
					"Student must be in Admission status to update admission general details.")
				.WithData("StudentId", studentId)
				.WithData("CurrentStatus", student.Status);
		}

		await UpdateAdmissionAcademicPlacementAsync(student, input);
		await studentScopeAccessService.EnsureCanUseInquiryPreferenceAsync(
			input.InquiryPreferenceId,
			student.AcademicYearId ?? throw new BusinessException(StudentRegistrationErrorCodes.InvalidStudentStatusForGeneralInfo),
			nameof(UpdateAdmissionGeneralDetailsAsync));

		student.UpdateGeneralInformation(input.FirstName, input.MiddleName, input.LastName);

		student.AdditionalDetail?.UpdateGeneralInformation(
			input.DateOfBirth,
			input.Gender,
			input.ProfilePictureUrl,
			null,
			null,
			input.PanNo,
			null,
			input.Religion,
			input.FamilyCast,
			input.CastCategory,
			input.Nationality,
			input.BirthPlace,
			input.MotherTongue,
			input.PreviousSchool,
			input.LastClassPassed,
			StudentRegistrationStateEnum.General,
			input.InquiryPreferenceId,
			input.FormNumber);

		student.ContactDetail?.UpdateContactInfo(
			input.PrimaryContactNo,
			input.SecondaryContactNo,
			input.Email,
			input.Address1,
			input.Address2,
			input.Address3,
			input.City,
			input.State,
			input.Country,
			input.Pincode);

		await studentRepository.UpdateAsync(student, autoSave: true);
		var compliance = await UpsertComplianceAsync(student.Id, input);

		var result = ObjectMapper.Map<Student, ShikshaMitra.Students.Enrollments.Registrations.StudentGeneralDetailsDto>(student);
		ApplyComplianceMask(result, compliance);

		return result;
	}

	private async Task UpdateAdmissionAcademicPlacementAsync(
		Student student,
		ShikshaMitra.Students.Enrollments.Registrations.UpdateGeneralInfoDto input)
	{
		if (!input.AcademicMediumId.HasValue &&
		    !input.StandardId.HasValue &&
		    !input.DivisionId.HasValue)
		{
			return;
		}

		var academicMediumId = input.AcademicMediumId ?? student.AcademicMediumId;
		var standardId = input.StandardId ?? student.StandardId
			?? throw new BusinessException(StudentRegistrationErrorCodes.StandardNotFound, "Standard is required");

		await validationService.ValidateAcademicMediumExistsAsync(academicMediumId);
		await validationService.ValidateStandardExistsAsync(standardId);

		var standard = await standardRepository.GetAsync(standardId, includeDetails: true)
			?? throw new BusinessException(StudentRegistrationErrorCodes.StandardNotFound, "Standard not found");

		if (standard.EducationMediumStage.EducationMedium.Id != academicMediumId)
		{
			throw new BusinessException(StudentRegistrationErrorCodes.StandardNotFound, "Standard does not belong to the specified Academic Medium");
		}

		await studentScopeAccessService.EnsureCanAccessStandardAsync(standardId, nameof(UpdateAdmissionGeneralDetailsAsync));

		Guid? divisionId = input.DivisionId;
		if (divisionId.HasValue)
		{
			await EnsureDivisionMatchesStandardAsync(
				divisionId.Value,
				standardId,
				nameof(UpdateAdmissionGeneralDetailsAsync));
		}
		else if (student.StandardId == standardId)
		{
			divisionId = student.DivisionId;
		}

		student.UpdateAcademicPlacement(
			standard.EducationMediumStage.EducationMedium.EducationBoard.AcademicYearId,
			standard.EducationMediumStage.EducationMedium.EducationBoardId,
			academicMediumId,
			standard.EducationMediumStageId,
			standardId,
			divisionId);
	}

	private async Task EnsureDivisionMatchesStandardAsync(Guid divisionId, Guid standardId, string operation)
	{
		var division = await divisionRepository.GetAsync(divisionId);
		if (division.StandardId != standardId)
		{
			throw new BusinessException(StudentRegistrationErrorCodes.StandardNotFound, "Division does not belong to the selected Standard");
		}

		await studentScopeAccessService.EnsureCanAccessDivisionAsync(divisionId, operation);
	}

	private Task<StudentIdentityComplianceDto> UpsertComplianceAsync(Guid studentId, RegistrationGeneralInfoBaseDto input) =>
		identityComplianceAppService.UpsertFromRegistrationAsync(studentId, new UpdateStudentIdentityComplianceDto
		{
			AadhaarNo = input.AadharNo,
			AadhaarName = BuildStudentName(input.FirstName, input.MiddleName, input.LastName),
			AadhaarVerificationStatus = string.IsNullOrWhiteSpace(input.AadharNo) ? IdentityVerificationStatus.NotProvided : IdentityVerificationStatus.Pending,
			ApaarId = input.ApparId,
			ApaarVerificationStatus = string.IsNullOrWhiteSpace(input.ApparId) ? IdentityVerificationStatus.NotProvided : IdentityVerificationStatus.Pending,
			PenNumber = input.PennNo,
			UdiseSyncStatus = UdiseSyncStatus.NotReady
		});

	private static void ApplyComplianceMask(
		ShikshaMitra.Students.Enrollments.Registrations.StudentGeneralDetailsDto result,
		StudentIdentityComplianceDto compliance)
	{
		result.AdharNo = compliance.AadhaarMasked;
		result.AadhaarMasked = compliance.AadhaarMasked;
		result.AadhaarVerificationStatus = compliance.AadhaarVerificationStatus;
		result.ApparId = compliance.ApaarId;
		result.ApaarConsentStatus = compliance.ApaarConsentStatus;
		result.ApaarVerificationStatus = compliance.ApaarVerificationStatus;
		result.PennNo = compliance.PenNumber;
		result.UdiseStudentCode = compliance.UdiseStudentCode;
		result.UdiseSyncStatus = compliance.UdiseSyncStatus;
		result.RteStatus = compliance.RteStatus;
		result.ScholarshipStatus = compliance.ScholarshipStatus;
	}

	private static string BuildStudentName(string firstName, string? middleName, string lastName) =>
		$"{firstName} {middleName} {lastName}".Replace("  ", " ").Trim();

	/// <summary>
	/// Changes admission status (approve/reject) for multiple students
	/// </summary>
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_Admission.Add)]
	public async Task<BatchAdmissionStatusChangeResultDto> ChangeStatusBatchAsync(BatchAdmissionStatusChangeRequestDto input)
	{
		var result = new BatchAdmissionStatusChangeResultDto();

		foreach (var item in input.Items)
		{
			var itemResult = new AdmissionStatusChangeItemResultDto
			{
				StudentId = item.StudentId
			};

			try
			{
				// Load only the aggregate sections needed for approval/account creation.
				var student = await studentRepository.GetInquiryForUpdateAsync(item.StudentId);

				if (student == null)
				{
					throw new Exception($"Student with ID {item.StudentId} does not exist.");
				}
				await userScopeAccessService.EnsureCurrentUserCanAccessAsync(
					student,
					operation: nameof(ChangeStatusBatchAsync));

				// Apply action
				if (item.Action == StudentEnrollmentApprovalState.Approved)
				{
					if (student.Status != StudentLifeCycleStatusEnum.Enrolled ||
					    student.AdmissionApprovalStatus != StudentEnrollmentApprovalState.Approved)
					{
						student.ApproveAdmission(item.Remarks);
					}

					// Create user account and send welcome email ONLY on approval
					await CreateStudentUserAndSendEmailAsync(student);

					// Create single parent account
					await CreateParentAccountAsync(student);

					await SyncAdmissionMembershipsAsync(student);

					itemResult.NewStatus = StudentEnrollmentApprovalState.Approved.ToString();
					itemResult.Message = "Admission approved successfully. Student and parent accounts created and welcome emails sent.";
				}
				else if (item.Action == StudentEnrollmentApprovalState.Rejected)
				{
					student.RejectAdmission(item.Remarks);
					itemResult.NewStatus = StudentEnrollmentApprovalState.Rejected.ToString();
					itemResult.Message = "Admission rejected successfully";
				}
				else
				{
					student.MoveToPendingAdmission(item.Remarks);
					itemResult.NewStatus = StudentEnrollmentApprovalState.Pending.ToString();
					itemResult.Message = "Admission moved to pending successfully";
				}

				// Save changes
				await studentRepository.UpdateAsync(student);

				itemResult.Success = true;
				result.Succeeded++;
			}
			catch (BusinessException ex)
			{
				// BusinessExceptions are user-facing and safe to expose
				itemResult.Success = false;
				itemResult.Message = ex.Message;
				result.Failed++;

				// Log the business exception for diagnostics
				Logger.LogWarning(ex, "Business exception occurred while processing admission status change for student {StudentId}", item.StudentId);
			}
			catch (Exception ex)
			{
				// Unexpected exceptions get a generic message to avoid leaking internal details
				itemResult.Success = false;
				itemResult.Message = "An unexpected error occurred while processing admission status.";
				result.Failed++;

				// Log the full exception for diagnostics
				Logger.LogError(ex, "Unexpected error occurred while processing admission status change for student {StudentId}", item.StudentId);
			}

			result.Results.Add(itemResult);
		}

		return result;
	}

	private async Task SyncAdmissionMembershipsAsync(Student student)
	{
		if (student.AdditionalDetail?.IsLibraryMember == true)
		{
			await libraryMemberAppService.HandleLibraryMemberAsync(student.Id, isActive: true);
		}

		if (student.AdditionalDetail?.IsTransportMember == true)
		{
			Logger.LogInformation(
				"Transport membership flag preserved for student {StudentId}; transport membership workflow is not available yet.",
				student.Id);
		}
	}

	private async Task CreateStudentUserAndSendEmailAsync(Student student)
	{
		// 1. Determine Username (GR Number is preferred for admitted students)
		var username = student.GrNo?.ToString() ?? await GenerateUniqueUsernameAsync(student.FirstName, student.LastName);

		// 2. Get Contact Details
		var email = student.ContactDetail?.Email;
		var mobile = student.ContactDetail?.PrimaryContactNo ?? "0000000000";

		if (string.IsNullOrWhiteSpace(email))
		{
			Logger.LogWarning("Cannot create user account or send email for student {StudentId} ({FirstName} {LastName}): Email is missing.", student.Id, student.FirstName, student.LastName);
			return;
		}

		// 3. Check if user already exists
		var existingUser = await userManager.FindByEmailAsync(email);
		if (existingUser != null)
		{
			Logger.LogInformation("User account for {Email} already exists. Skipping creation.", email);
			await TryAddUserToRoleAsync(existingUser, "Student", "student", student.Id);
			return;
		}

		// 4. Generate Standard Password (Username@last4mobile - with uppercase first letter)
		var last4Digits = mobile.Length >= 4 ? mobile.Substring(mobile.Length - 4) : "0000";
		var capitalizedUsername = char.ToUpper(username[0]) + username.Substring(1);
		var password = $"{capitalizedUsername}@{last4Digits}";

		// 5. Create IdentityUser
		var user = new IdentityUser(
			guidGenerator.Create(),
			username,
			email,
			CurrentTenant.Id
		)
		{
			Name = student.FirstName,
			Surname = student.LastName
		};
		user.SetEmailConfirmed(true);

		var result = await userManager.CreateAsync(user, password);
		if (result.Succeeded)
		{
			await TryAddUserToRoleAsync(user, "Student", "student", student.Id);

			var studentFullName = $"{student.FirstName} {student.LastName}".Trim();
			await TrySendAdmissionApprovalEmailAsync(email, studentFullName, username, password, student.Id);

			Logger.LogInformation("Successfully created user account and sent admission email for student {StudentId}", student.Id);
		}
		else
		{
			var errors = string.Join(", ", result.Errors.Select(e => e.Description));
			Logger.LogError("Failed to create user account for student {StudentId}. Errors: {Errors}", student.Id, errors);
			throw new BusinessException("StudentAdmission:UserAccountCreationFailed").WithData("Errors", errors);
		}
	}

	private async Task<string> GenerateUniqueUsernameAsync(string firstName, string lastName)
	{
		var baseUsername = $"{firstName.ToLower()}{lastName.ToLower()}".Replace(" ", "");
		var username = baseUsername;
		var counter = 1;

		while (await userManager.FindByNameAsync(username) != null)
		{
			username = $"{baseUsername}{counter++}";
		}

		return username;
	}

	/// <summary>
	/// Creates a single parent account using the first available parent with valid email
	/// Priority: Father -> Mother -> Guardian
	/// </summary>
	private async Task CreateParentAccountAsync(Student student)
	{
		if (student.FamilyDetail == null)
		{
			Logger.LogInformation("No family details found for student {StudentId}. Skipping parent account creation.", student.Id);
			return;
		}

		// Find the first parent with a valid email (Priority: Father -> Mother -> Guardian)
		ParentGuardianDetails? parentDetails = null;
		string parentType = string.Empty;

		if (!string.IsNullOrWhiteSpace(student.FamilyDetail.FatherDetails?.Email))
		{
			parentDetails = student.FamilyDetail.FatherDetails;
			parentType = "Father";
		}
		else if (!string.IsNullOrWhiteSpace(student.FamilyDetail.MotherDetails?.Email))
		{
			parentDetails = student.FamilyDetail.MotherDetails;
			parentType = "Mother";
		}
		else if (!string.IsNullOrWhiteSpace(student.FamilyDetail.GuardianDetails?.Email))
		{
			parentDetails = student.FamilyDetail.GuardianDetails;
			parentType = "Guardian";
		}

		if (parentDetails == null)
		{
			Logger.LogInformation("No parent with valid email found for student {StudentId}. Skipping parent account creation.", student.Id);
			return;
		}

		try
		{
			var email = parentDetails.Email!;
			var fullName = parentDetails.FullName ?? parentType;
			var mobile = parentDetails.ContactNo ?? "0000000000";

			// Check if user already exists
			var existingUser = await userManager.FindByEmailAsync(email);
			if (existingUser != null)
			{
				Logger.LogInformation("Parent account for {Email} already exists. Skipping creation.", email);
				await TryAddUserToRoleAsync(existingUser, "Parent", "parent", student.Id);
				return;
			}

			// Generate username from parent name
			var nameParts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			var firstName = nameParts.Length > 0 ? nameParts[0] : "parent";
			var lastName = nameParts.Length > 1 ? nameParts[^1] : "user";
			var username = await GenerateUniqueUsernameAsync(firstName, lastName);

			// Generate password (with uppercase first letter)
			var last4Digits = mobile.Length >= 4 ? mobile.Substring(mobile.Length - 4) : "0000";
			var capitalizedUsername = char.ToUpper(username[0]) + username.Substring(1);
			var password = $"{capitalizedUsername}@{last4Digits}";

			// Create IdentityUser
			var user = new IdentityUser(
				guidGenerator.Create(),
				username,
				email,
				CurrentTenant.Id
			)
			{
				Name = firstName,
				Surname = lastName
			};
			user.SetEmailConfirmed(true);

			var result = await userManager.CreateAsync(user, password);
			if (result.Succeeded)
			{
				await TryAddUserToRoleAsync(user, "Parent", "parent", student.Id);

				var studentFullName = $"{student.FirstName} {student.LastName}".Trim();
				await TrySendAdmissionApprovalEmailAsync(
					email,
					$"{fullName} ({parentType} of {studentFullName})",
					username,
					password,
					student.Id);

				Logger.LogInformation("Successfully created parent account ({ParentType}) for student {StudentId}", parentType, student.Id);
			}
			else
			{
				var errors = string.Join(", ", result.Errors.Select(e => e.Description));
				Logger.LogWarning("Failed to create parent account for student {StudentId}. Errors: {Errors}", student.Id, errors);
			}
		}
		catch (Exception ex)
		{
			// Log but don't fail the student admission if parent account creation fails
			Logger.LogWarning(ex, "Error creating parent account for student {StudentId}", student.Id);
		}
	}

	private async Task TryAddUserToRoleAsync(IdentityUser user, string roleName, string accountType, Guid studentId)
	{
		try
		{
			if (await userManager.IsInRoleAsync(user, roleName))
			{
				return;
			}

			var result = await userManager.AddToRoleAsync(user, roleName);
			if (!result.Succeeded)
			{
				Logger.LogWarning(
					"Could not assign {RoleName} role to {AccountType} account for student {StudentId}. Errors: {Errors}",
					roleName,
					accountType,
					studentId,
					string.Join(", ", result.Errors.Select(error => error.Description)));
			}
		}
		catch (Exception ex)
		{
			Logger.LogWarning(
				ex,
				"Could not assign {RoleName} role to {AccountType} account for student {StudentId}. Admission approval will continue.",
				roleName,
				accountType,
				studentId);
		}
	}

	private async Task TrySendAdmissionApprovalEmailAsync(
		string email,
		string displayName,
		string username,
		string password,
		Guid studentId)
	{
		try
		{
			await studentEmailService.SendAdmissionApprovalEmailAsync(email, displayName, username, password);
		}
		catch (Exception ex)
		{
			Logger.LogWarning(
				ex,
				"Could not send admission approval email to {Email} for student {StudentId}. Admission approval will continue.",
				email,
				studentId);
		}
	}
}
