using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Portal;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public partial class StudentOnboardingAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	public async Task<IReadOnlyList<StudentPortalAccountDto>> GetPortalAccountsAsync(Guid id)
	{
		var item = await GetCaseAsync(id);
		return await MapPortalLinksAsync(await portalLinkRepository.GetListAsync(x => x.StudentId == item.StudentId));
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit)]
	[Authorize(IdentityPermissions.Users.Create)]
	[UnitOfWork(true)]
	public async Task<StudentPortalAccountDto> ProvisionPortalAccountAsync(Guid id, ProvisionStudentPortalAccountDto input)
	{
		// Repeat validation for direct application calls, not just HTTP model binding.
		var validation = await new ProvisionStudentPortalAccountDtoValidator().ValidateAsync(input);
		if (!validation.IsValid) throw PortalInvalid(validation.Errors[0].ErrorMessage);
		if (!CurrentTenant.Id.HasValue || !CurrentUser.Id.HasValue) throw new AbpAuthorizationException();
		var item = await GetCaseAsync(id);
		var existingLink = await portalLinkRepository.FindAsync(x => x.StudentId == item.StudentId && x.AccountType == input.AccountType);
		if (existingLink != null)
		{
			var linkedUser = await identityUserRepository.GetAsync(existingLink.UserId);
			if (!linkedUser.IsActive || !string.Equals(linkedUser.Email, input.Email.Trim(), StringComparison.OrdinalIgnoreCase) ||
				existingLink.Relationship != input.Relationship)
				throw PortalInvalid("The existing account link differs or is inactive. Use the identity administrator's reviewed repair workflow.");
			return (await MapPortalLinksAsync([existingLink]))[0];
		}
		EnsureConcurrency(item, input.ConcurrencyStamp);
		if (!item.StudentEnrollmentId.HasValue || item.Status is StudentOnboardingStatus.Completed or StudentOnboardingStatus.Cancelled)
			throw new BusinessException(StudentOnboardingErrorCodes.CaseNotEditable);
		var handoff = await handoffRepository.FindAsync(x => x.OnboardingCaseId == id && x.ServiceKey == input.AccountType + "-account")
			?? throw PortalInvalid("This account is not required by the saved onboarding settings.");
		var student = await studentRepository.GetWithDetailsAsync(item.StudentId)
			?? throw PortalInvalid("Student record is unavailable.");
		var guardian = input.Relationship switch
		{
			"father" => student.FamilyDetail?.FatherDetails,
			"mother" => student.FamilyDetail?.MotherDetails,
			"guardian" => student.FamilyDetail?.GuardianDetails,
			_ => null
		};
		var email = input.Email.Trim();
		var recordedEmail = input.AccountType == "student" ? student.ContactDetail?.Email : guardian?.Email;
		if (!string.Equals(email, recordedEmail?.Trim(), StringComparison.OrdinalIgnoreCase))
			throw PortalInvalid("The email must match the selected person's saved profile. Correct and verify the profile first.");
		var role = input.AccountType == "student" ? "Student" : "Parent";
		if (!await portalRoleManager.RoleExistsAsync(role))
			throw PortalInvalid("The required portal role has not been configured by an identity administrator.");
		var user = await portalUserManager.FindByEmailAsync(email);
		if (user != null)
		{
			// Existing staff identities and unverified email matches are never silently repurposed.
			await AuthorizationService.CheckAsync(IdentityPermissions.Users.Update);
			var roles = await portalUserManager.GetRolesAsync(user);
			if (!input.LinkExistingGuardian || input.AccountType != "guardian" || !user.IsActive || !user.EmailConfirmed ||
				roles.Count != 1 || !roles.Contains("Parent") ||
				!await portalLinkRepository.AnyAsync(x => x.UserId == user.Id && x.AccountType == "guardian"))
				throw PortalInvalid("An identity already uses this email. Only an explicitly confirmed, verified existing guardian portal account may be linked for a sibling.");
		}
		else
		{
			user = new IdentityUser(GuidGenerator.Create(), email, email, CurrentTenant.Id)
			{
				Name = input.AccountType == "student" ? student.FirstName : guardian?.FullName,
				Surname = input.AccountType == "student" ? student.LastName : null
			};
			// No password is generated, returned, logged, or sent. The owner uses the normal
			// verified email/password-setup flow. Never mark an unverified mailbox confirmed.
			var created = await portalUserManager.CreateAsync(user);
			if (!created.Succeeded) throw PortalInvalid("Account creation was rejected by the identity policy. Review the email and username with your identity administrator.");
			var assigned = await portalUserManager.AddToRoleAsync(user, role);
			if (!assigned.Succeeded) throw PortalInvalid("The configured portal role could not be assigned.");
		}
		var link = new StudentPortalAccountLink(GuidGenerator.Create(), CurrentTenant.Id.Value, student.Id,
			user.Id, input.AccountType, input.Relationship, CurrentUser.Id.Value);
		await portalLinkRepository.InsertAsync(link, false);
		handoff.Acknowledge(DateTime.UtcNow);
		await handoffRepository.UpdateAsync(handoff, false);
		await AddEventAsync(id, "PortalAccountLinked", $"Staff verified and linked the {input.AccountType} account ({input.Relationship}). Owner email verification and password setup remain separate.");
		await CurrentUnitOfWork!.SaveChangesAsync();
		return (await MapPortalLinksAsync([link]))[0];
	}

	[Authorize]
	public async Task<IReadOnlyList<StudentPortalAccountDto>> GetMyPortalStudentsAsync()
	{
		return await portalAccess.GetMyStudentsAsync();
	}

	private async Task<IReadOnlyList<StudentPortalAccountDto>> MapPortalLinksAsync(IEnumerable<StudentPortalAccountLink> links)
	{
		var result = new List<StudentPortalAccountDto>();
		foreach (var link in links)
		{
			var user = await identityUserRepository.FindAsync(link.UserId);
			var student = await studentRepository.FindAsync(link.StudentId);
			if (user == null || student == null) continue;
			result.Add(new StudentPortalAccountDto { StudentId = student.Id, UserId = user.Id,
				StudentName = string.Join(" ", new[] { student.FirstName, student.LastName }.Where(x => !string.IsNullOrWhiteSpace(x))),
				AccountType = link.AccountType, Relationship = link.Relationship, Email = user.Email,
				EmailConfirmed = user.EmailConfirmed, IsActive = user.IsActive });
		}
		return result;
	}

	private static BusinessException PortalInvalid(string reason) =>
		new BusinessException(StudentOnboardingErrorCodes.PortalAccountInvalid).WithData("Reason", reason);
}
