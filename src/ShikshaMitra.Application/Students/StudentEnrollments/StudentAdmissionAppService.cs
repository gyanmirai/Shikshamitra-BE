using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Admissions;
using ShikshaMitra.Students.Enrollments.Admissions;
using ShikshaMitra.TenantSettings.Divisions;
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

namespace ShikshaMitra.Students.StudentEnrollments;

/// <summary>
/// Application service for Student Admission operations
/// </summary>
[Authorize]
[RemoteService(false)]
public class StudentAdmissionAppService(
	IRepository<Student, Guid> studentRepository,
	IRepository<Division, Guid> divisionRepository,
	IdentityUserManager userManager,
	IGuidGenerator guidGenerator,
	IStudentEmailService studentEmailService,
	AdmissionManager admissionManager,
	IUserScopeAccessService userScopeAccessService,
	StudentScopeAccessService studentScopeAccessService) : ApplicationService, IStudentAdmissionAppService
{
	/// <summary>
	/// Gets a paginated list of students in admission stage with filters
	/// </summary>
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_Admission.View)]
	public async Task<PagedResultDto<StudentDto>> GetListAsync(GetStudentAdmissionsInput input)
	{
		var query = (await studentRepository.GetQueryableAsync())
			.Where(s => s.Status == StudentLifeCycleStatusEnum.Admission);

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

		// Apply sorting and paging
		query = query.OrderByDescending(s => s.CreationTime)
			.PageBy(input.SkipCount, input.MaxResultCount);

		// Execute query and project to DTO
		var students = await AsyncExecuter.ToListAsync(
			query
		);

		var studentDtos = ObjectMapper.Map<List<Student>, List<StudentDto>>(students);

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
				// Get student with contact details for account creation and email
				var queryable = await studentRepository.WithDetailsAsync(x => x.ContactDetail, x => x.FamilyDetail);
				var student = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x => x.Id == item.StudentId));

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
					student.ApproveAdmission();

					// Create user account and send welcome email ONLY on approval
					await CreateStudentUserAndSendEmailAsync(student);

					// Create single parent account
					await CreateParentAccountAsync(student);

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
			// Assign Student role
			await userManager.AddToRoleAsync(user, "Student");

			// Send Admission Approved Email
			var studentFullName = $"{student.FirstName} {student.LastName}".Trim();
			await studentEmailService.SendAdmissionApprovalEmailAsync(email, studentFullName, username, password);

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
				// Assign Parent role
				await userManager.AddToRoleAsync(user, "Parent");

				// Send welcome email
				var studentFullName = $"{student.FirstName} {student.LastName}".Trim();
				await studentEmailService.SendAdmissionApprovalEmailAsync(
					email,
					$"{fullName} ({parentType} of {studentFullName})",
					username,
					password
				);

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
}
