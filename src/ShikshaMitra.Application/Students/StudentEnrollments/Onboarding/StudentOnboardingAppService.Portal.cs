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
		return await ProvisionPortalAccountCoreAsync(id, input);
	}

	// Only trusted, scoped commands call this core after authorizing the saved account owner.
	private async Task<StudentPortalAccountDto> ProvisionPortalAccountCoreAsync(Guid id,
		ProvisionStudentPortalAccountDto input, bool credentialsRequested = false)
	{
		if (!CurrentTenant.Id.HasValue || !CurrentUser.Id.HasValue) throw new AbpAuthorizationException();
		var item = await GetCaseAsync(id);
		var existingLink = await portalLinkRepository.FindAsync(x => x.StudentId == item.StudentId && x.AccountType == input.AccountType);
		if (ShikshaMitra.Students.Records.StudentRecordAdmissionReturnService.ReturnTransitionId(item).HasValue)
			await LazyServiceProvider.LazyGetRequiredService<ShikshaMitra.Students.Records.StudentRecordAdmissionReturnService>().ValidateOnboardingAsync(item, false);
		if (existingLink != null)
		{
			var linkedUser = await identityUserRepository.GetAsync(existingLink.UserId);
			if (!linkedUser.IsActive || !string.Equals(linkedUser.Email, input.Email.Trim(), StringComparison.OrdinalIgnoreCase) ||
				existingLink.Relationship != input.Relationship)
				throw PortalInvalid("The existing account link differs or is inactive. Use the identity administrator's reviewed repair workflow.");
			await QueueAutomaticCredentialEmailsAsync(item);
			return (await MapPortalLinksAsync([existingLink]))[0];
		}
		EnsureConcurrency(item, input.ConcurrencyStamp);
		if (!item.StudentEnrollmentId.HasValue || item.Status is StudentOnboardingStatus.Completed or StudentOnboardingStatus.Cancelled)
			throw new BusinessException(StudentOnboardingErrorCodes.CaseNotEditable);
		var handoff = await handoffRepository.FindAsync(x => x.OnboardingCaseId == id && x.ServiceKey == input.AccountType + "-account")
			?? throw PortalInvalid("This account is not required by the saved onboarding settings.");
		var student = await studentRepository.GetWithDetailsAsync(item.StudentId)
			?? throw PortalInvalid("Student record is unavailable.");
		if (input.AccountType == "guardian" && !StudentPortalOwnerSelection.Relationships(student.FamilyDetail).Contains(input.Relationship))
			throw PortalInvalid("Choose a saved Father or Mother profile. Other guardian is available only when neither parent is recorded.");
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
			if (!input.LinkExistingGuardian || input.AccountType != "guardian" || !user.IsActive ||
				roles.Count != 1 || !roles.Contains("Parent") ||
				!await LazyServiceProvider.LazyGetRequiredService<StudentPortalSiblingIdentityService>().HasVerifiedGuardianIdentityAsync(user))
				throw PortalInvalid("An identity already uses this email. Only an explicitly confirmed, verified existing guardian portal account may be linked for a sibling.");
		}
		else
		{
			user = new IdentityUser(GuidGenerator.Create(), email, email, CurrentTenant.Id)
			{
				Name = input.AccountType == "student" ? student.FirstName : guardian?.FullName,
				Surname = input.AccountType == "student" ? student.LastName : null
			};
			// Credential delivery later prepares a random initial password transactionally.
			// A manual account without email delivery retains the owner password-setup flow.
			var created = await portalUserManager.CreateAsync(user);
			if (!created.Succeeded) throw PortalInvalid("Account creation was rejected by the identity policy. Review the email and username with your identity administrator.");
			var assigned = await portalUserManager.AddToRoleAsync(user, role);
			if (!assigned.Succeeded) throw PortalInvalid("The configured portal role could not be assigned.");
			var mobile = input.AccountType == "student" ? student.ContactDetail?.PrimaryContactNo : guardian?.ContactNo;
			if (!string.IsNullOrWhiteSpace(mobile))
			{
				var phone = await portalUserManager.SetPhoneNumberAsync(user, mobile.Trim());
				if (!phone.Succeeded) throw PortalInvalid("The saved account-owner mobile could not be assigned.");
			}
		}
		var link = new StudentPortalAccountLink(GuidGenerator.Create(), CurrentTenant.Id.Value, student.Id,
			user.Id, input.AccountType, input.Relationship, CurrentUser.Id.Value);
		await portalLinkRepository.InsertAsync(link, false);
		handoff.Acknowledge(DateTime.UtcNow);
		await handoffRepository.UpdateAsync(handoff, false);
		await AddEventAsync(id, "PortalAccountLinked", credentialsRequested
			? $"Staff authorized portal access and requested credentials for the saved {input.AccountType} owner ({input.Relationship}). Owner email verification and password setup remain separate."
			: $"Staff verified and linked the {input.AccountType} account ({input.Relationship}). Owner email verification and password setup remain separate.");
		// The identity manager writes through the same unit of work. Flush before the
		// completion check so it can read the just-created active user and link; this
		// remains transactional and is rolled back if a later operation fails.
		await CurrentUnitOfWork!.SaveChangesAsync();
		// A portal link is a local access handoff. Re-evaluate the requested account
		// handoffs immediately; credential delivery still follows the saved timing.
		await RefreshProvisioningAsync(item);
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

	// Use the same current profile fields as ProvisionPortalAccountAsync, never admission snapshots.
	private static IReadOnlyList<StudentPortalAccountOwnerDto> MapPortalAccountOwners(Student student)
	{
		var relationships = StudentPortalOwnerSelection.Relationships(student.FamilyDetail);
		return new StudentPortalAccountOwnerDto[]
		{
			new() { AccountType = "student", Relationship = "self", Name = $"{student.FirstName} {student.LastName}".Trim(), Email = student.ContactDetail?.Email?.Trim() },
			new() { AccountType = "guardian", Relationship = "father", Name = student.FamilyDetail?.FatherDetails?.FullName, Email = student.FamilyDetail?.FatherDetails?.Email?.Trim() },
			new() { AccountType = "guardian", Relationship = "mother", Name = student.FamilyDetail?.MotherDetails?.FullName, Email = student.FamilyDetail?.MotherDetails?.Email?.Trim() },
			new() { AccountType = "guardian", Relationship = "guardian", Name = student.FamilyDetail?.GuardianDetails?.FullName, Email = student.FamilyDetail?.GuardianDetails?.Email?.Trim() }
		}.Where(owner => owner.AccountType == "student" || relationships.Contains(owner.Relationship)).ToArray();
	}

	private static BusinessException PortalInvalid(string reason) =>
		new BusinessException(StudentOnboardingErrorCodes.PortalAccountInvalid).WithData("Reason", reason);
}
