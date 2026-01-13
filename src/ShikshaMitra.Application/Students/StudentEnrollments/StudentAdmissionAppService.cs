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
using System.Runtime.Intrinsics.Arm;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.ObjectMapping;
using Volo.Abp.Identity;
using Volo.Abp.Guids;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Students.StudentEnrollments;

/// <summary>
/// Application service for Student Admission operations
/// </summary>
[Authorize]
[RemoteService(false)]
public class StudentAdmissionAppService : ApplicationService, IStudentAdmissionAppService
{
	private readonly IRepository<Student, Guid> studentRepository;
	private readonly IRepository<Division, Guid> divisionRepository;
	private readonly IdentityUserManager userManager;
	private readonly IGuidGenerator guidGenerator;
	private readonly IStudentEmailService studentEmailService;

	public StudentAdmissionAppService(
		IRepository<Student, Guid> studentRepository,
		IRepository<Division, Guid> divisionRepository,
		IdentityUserManager userManager,
		IGuidGenerator guidGenerator,
		IStudentEmailService studentEmailService)
	{
		this.studentRepository = studentRepository;
		this.divisionRepository = divisionRepository;
		this.userManager = userManager;
		this.guidGenerator = guidGenerator;
		this.studentEmailService = studentEmailService;
	}

	/// <summary>
	/// Gets a paginated list of students in admission stage with filters
	/// </summary>
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_Admission.View)]
	public async Task<PagedResultDto<StudentEnrollmentListDto>> GetListAsync(GetStudentAdmissionsInput input)
	{
		var query = (await studentRepository.GetQueryableAsync())
			.Where(s => s.Status == StudentLifeCycleStatusEnum.Admission);

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
			.Skip(input.SkipCount)
			.Take(input.MaxResultCount);

		// Execute query and project to DTO
		var students = await studentRepository.AsyncExecuter.ToListAsync(
			query
		);

		var studentDtos = ObjectMapper.Map<List<Student>, List<StudentEnrollmentListDto>>(students);

		return new PagedResultDto<StudentEnrollmentListDto>(totalCount, studentDtos);
	}

	/// <summary>
	/// Assigns or changes division for a student
	/// </summary>
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_Admission.Add)]
	public async Task AssignDivisionAsync(Guid studentId, AssignDivisionDto input)
	{
		// Validate division exists
		var divisionExists = await divisionRepository.AnyAsync(d => d.Id == input.DivisionId);
		if (!divisionExists)
		{
			throw new BusinessException("StudentAdmission:DivisionNotFound")
				.WithData("DivisionId", input.DivisionId);
		}

		// Get student
		var student = await studentRepository.GetAsync(studentId);

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
				var queryable = await studentRepository.WithDetailsAsync(x => x.ContactDetail);
				var student = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x => x.Id == item.StudentId));

				if (student == null)
				{
					throw new EntityNotFoundException(typeof(Student), item.StudentId);
				}

				// Apply action
				if (item.Action == AdmissionAction.Approve)
				{
					student.ApproveAdmission();
					
					// Create user account and send welcome email ONLY on approval
					await CreateStudentUserAndSendEmailAsync(student);

					itemResult.NewStatus = StudentEnrollmentApprovalState.Approved.ToString();
					itemResult.Message = "Admission approved successfully. Student account created and welcome email sent.";
				}
				else if (item.Action == AdmissionAction.Reject)
				{
					if (string.IsNullOrWhiteSpace(item.Remarks))
					{
						throw new BusinessException("StudentAdmission:RemarksRequired")
							.WithData("StudentId", item.StudentId);
					}

					student.RejectAdmission(item.Remarks);
					itemResult.NewStatus = StudentEnrollmentApprovalState.Rejected.ToString();
					itemResult.Message = "Admission rejected successfully";
				}
				else
				{
					// Explicitly handle invalid/unsupported actions
					throw new BusinessException("StudentAdmission:InvalidAction")
						.WithData("StudentId", item.StudentId)
						.WithData("Action", item.Action);
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

		// 4. Generate Standard Password (username@last4mobile)
		var last4Digits = mobile.Length >= 4 ? mobile.Substring(mobile.Length - 4) : "0000";
		var password = $"{username}@{last4Digits}";

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
}
