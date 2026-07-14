using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.Enrollments.Admissions;
using ShikshaMitra.Students.StudentRecords;
using ShikshaMitra.Students.ValueObjects;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.Identity;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public sealed record AdmissionCrmStudentActivationOutcome(bool Success, string Message);

public class AdmissionCrmStudentActivationOrchestrator : ITransientDependency
{
	private readonly IStudentRepository _studentRepository;
	private readonly IdentityUserManager _userManager;
	private readonly IGuidGenerator _guidGenerator;
	private readonly IStudentEmailService _studentEmailService;
	private readonly IUserScopeAccessService _userScopeAccessService;
	private readonly ILogger<AdmissionCrmStudentActivationOrchestrator> _logger;

	public AdmissionCrmStudentActivationOrchestrator(
		IStudentRepository studentRepository,
		IdentityUserManager userManager,
		IGuidGenerator guidGenerator,
		IStudentEmailService studentEmailService,
		IUserScopeAccessService userScopeAccessService,
		ILogger<AdmissionCrmStudentActivationOrchestrator> logger)
	{
		_studentRepository = studentRepository;
		_userManager = userManager;
		_guidGenerator = guidGenerator;
		_studentEmailService = studentEmailService;
		_userScopeAccessService = userScopeAccessService;
		_logger = logger;
	}

	public async Task<AdmissionCrmStudentActivationOutcome> ActivateAsync(
		Guid studentId,
		bool requireStudentAccount,
		bool requireGuardianAccount,
		string remarks)
	{
		var student = await _studentRepository.GetInquiryForUpdateAsync(studentId);
		if (student == null)
		{
			return new(false, "The Student record no longer exists.");
		}

		await _userScopeAccessService.EnsureCurrentUserCanAccessAsync(student, nameof(ActivateAsync));

		try
		{
			var alreadyApproved = student.Status == StudentLifeCycleStatusEnum.Enrolled &&
				student.AdmissionApprovalStatus == StudentEnrollmentApprovalState.Approved;
			if (!alreadyApproved)
			{
				student.ApproveAdmission(remarks);
			}
			else
			{
				student.UpdateAdmissionApprovalRemarks(remarks);
			}

			if (requireStudentAccount)
			{
				await EnsureStudentAccountAsync(student);
			}

			if (requireGuardianAccount)
			{
				await EnsureGuardianAccountAsync(student);
			}

			await _studentRepository.UpdateAsync(student, autoSave: true);
			return new(true, "Enrollment and requested accounts are active.");
		}
		catch (BusinessException exception)
		{
			_logger.LogWarning(exception, "CRM activation failed for student {StudentId}", studentId);
			return new(false, exception.Code ?? exception.Message);
		}
		catch (Exception exception)
		{
			_logger.LogError(exception, "Unexpected CRM activation failure for student {StudentId}", studentId);
			return new(false, "Student account or enrollment activation failed.");
		}
	}

	private async Task EnsureStudentAccountAsync(Student student)
	{
		var email = student.ContactDetail?.Email;
		if (string.IsNullOrWhiteSpace(email))
		{
			throw new BusinessException("AdmissionCrm:StudentAccountEmailRequired");
		}

		var existingUser = await _userManager.FindByEmailAsync(email);
		if (existingUser != null)
		{
			await EnsureRoleAsync(existingUser, "Student", student.Id);
			return;
		}

		var username = student.GrNo?.ToString() ?? await GenerateUniqueUsernameAsync(student.FirstName, student.LastName);
		var password = BuildInitialPassword(username, student.ContactDetail?.PrimaryContactNo);
		var user = new IdentityUser(_guidGenerator.Create(), username, email, student.TenantId)
		{
			Name = student.FirstName,
			Surname = student.LastName
		};
		user.SetEmailConfirmed(true);
		var result = await _userManager.CreateAsync(user, password);
		if (!result.Succeeded)
		{
			throw new BusinessException("AdmissionCrm:StudentAccountCreationFailed")
				.WithData("Errors", string.Join(", ", result.Errors.Select(error => error.Description)));
		}

		await EnsureRoleAsync(user, "Student", student.Id);
		await TrySendCredentialsAsync(email, $"{student.FirstName} {student.LastName}".Trim(), username, password, student.Id);
	}

	private async Task EnsureGuardianAccountAsync(Student student)
	{
		var guardian = SelectGuardian(student);
		if (guardian.Details == null || string.IsNullOrWhiteSpace(guardian.Details.Email))
		{
			throw new BusinessException("AdmissionCrm:GuardianAccountEmailRequired");
		}

		var email = guardian.Details.Email;
		var existingUser = await _userManager.FindByEmailAsync(email);
		if (existingUser != null)
		{
			await EnsureRoleAsync(existingUser, "Parent", student.Id);
			return;
		}

		var fullName = guardian.Details.FullName ?? guardian.Type;
		var nameParts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		var firstName = nameParts.FirstOrDefault() ?? "parent";
		var lastName = nameParts.Length > 1 ? nameParts[^1] : "user";
		var username = await GenerateUniqueUsernameAsync(firstName, lastName);
		var password = BuildInitialPassword(username, guardian.Details.ContactNo);
		var user = new IdentityUser(_guidGenerator.Create(), username, email, student.TenantId)
		{
			Name = firstName,
			Surname = lastName
		};
		user.SetEmailConfirmed(true);
		var result = await _userManager.CreateAsync(user, password);
		if (!result.Succeeded)
		{
			throw new BusinessException("AdmissionCrm:GuardianAccountCreationFailed")
				.WithData("Errors", string.Join(", ", result.Errors.Select(error => error.Description)));
		}

		await EnsureRoleAsync(user, "Parent", student.Id);
		var studentName = $"{student.FirstName} {student.LastName}".Trim();
		await TrySendCredentialsAsync(email, $"{fullName} ({guardian.Type} of {studentName})", username, password, student.Id);
	}

	private static (ParentGuardianDetails? Details, string Type) SelectGuardian(Student student)
	{
		if (!string.IsNullOrWhiteSpace(student.FamilyDetail?.FatherDetails?.Email))
		{
			return (student.FamilyDetail.FatherDetails, "Father");
		}

		if (!string.IsNullOrWhiteSpace(student.FamilyDetail?.MotherDetails?.Email))
		{
			return (student.FamilyDetail.MotherDetails, "Mother");
		}

		return (student.FamilyDetail?.GuardianDetails, "Guardian");
	}

	private async Task<string> GenerateUniqueUsernameAsync(string firstName, string lastName)
	{
		var baseUsername = $"{firstName.ToLowerInvariant()}{lastName.ToLowerInvariant()}".Replace(" ", string.Empty);
		var username = string.IsNullOrWhiteSpace(baseUsername) ? $"student{_guidGenerator.Create():N}"[..15] : baseUsername;
		var counter = 1;
		while (await _userManager.FindByNameAsync(username) != null)
		{
			username = $"{baseUsername}{counter++}";
		}

		return username;
	}

	private static string BuildInitialPassword(string username, string? mobile)
	{
		var normalizedMobile = string.IsNullOrWhiteSpace(mobile) ? "0000" : mobile;
		var lastFour = normalizedMobile.Length >= 4 ? normalizedMobile[^4..] : "0000";
		var normalizedUsername = char.ToUpperInvariant(username[0]) + username[1..];
		return $"{normalizedUsername}@{lastFour}";
	}

	private async Task EnsureRoleAsync(IdentityUser user, string roleName, Guid studentId)
	{
		if (await _userManager.IsInRoleAsync(user, roleName))
		{
			return;
		}

		var result = await _userManager.AddToRoleAsync(user, roleName);
		if (!result.Succeeded)
		{
			throw new BusinessException("AdmissionCrm:AccountRoleAssignmentFailed")
				.WithData("StudentId", studentId)
				.WithData("Role", roleName)
				.WithData("Errors", string.Join(", ", result.Errors.Select(error => error.Description)));
		}
	}

	private async Task TrySendCredentialsAsync(string email, string displayName, string username, string password, Guid studentId)
	{
		try
		{
			await _studentEmailService.SendAdmissionApprovalEmailAsync(email, displayName, username, password);
		}
		catch (Exception exception)
		{
			_logger.LogWarning(exception, "Account created but credentials could not be sent for student {StudentId}", studentId);
		}
	}
}
