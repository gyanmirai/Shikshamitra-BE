using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Authorization;
using Volo.Abp.Identity;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public partial class StudentOnboardingAppService
{
	private async Task PrepareAutomaticPortalAccountsAsync(StudentOnboardingCase item)
	{
		var settings = await configurationManager.GetAsync(item.AcademicYearId);
		if (settings == null || !item.StudentEnrollmentId.HasValue || item.Status is StudentOnboardingStatus.Completed or StudentOnboardingStatus.Cancelled) return;
		var rules = settings.Readiness(item);
		if (OnboardingRuleConfiguration.Text(rules, "credentialProvisioningMode", "manual") != "automatic" ||
			!await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit) ||
			!await AuthorizationService.IsGrantedAsync(IdentityPermissions.Users.Create)) return;
		var student = await studentRepository.GetWithDetailsAsync(item.StudentId);
		if (student == null) return;
		var resolver = LazyServiceProvider.LazyGetRequiredService<IStudentCredentialEmailEligibilityResolver>();
		foreach (var type in new[] { "student", "guardian" })
		{
			if (!OnboardingRuleConfiguration.Boolean(rules, type == "student" ? "sendStudentCredentialEmail" : "sendGuardianCredentialEmail")) continue;
			if (await portalLinkRepository.AnyAsync(link => link.StudentId == student.Id && link.AccountType == type)) continue;
			// The resolver checks committed placement and current documents before it reports a missing account.
			var eligibility = await resolver.ResolveAsync(item.Id, type);
			if (eligibility.BlockedReason != StudentOnboardingCredentialEligibilityResolver.AccountRequired) continue;
			var relationship = type == "student" ? "self" : OnboardingRuleConfiguration.Text(rules, "guardianAccountRelationship", "guardian");
			if (type == "guardian" && !item.GuardianContactVerifiedAt.HasValue) continue;
			var email = type == "student" ? student.ContactDetail?.Email : relationship switch
			{
				"father" => student.FamilyDetail?.FatherDetails?.Email,
				"mother" => student.FamilyDetail?.MotherDetails?.Email,
				_ => student.FamilyDetail?.GuardianDetails?.Email
			};
			if (string.IsNullOrWhiteSpace(email)) continue;
			var user = await portalUserManager.FindByEmailAsync(email.Trim());
			if (user != null)
			{
				var roles = await portalUserManager.GetRolesAsync(user);
				if (type != "guardian" || !user.IsActive || roles.Count != 1 || roles[0] != "Parent" ||
					!await AuthorizationService.IsGrantedAsync(IdentityPermissions.Users.Update) ||
					!await LazyServiceProvider.LazyGetRequiredService<ShikshaMitra.Students.Portal.StudentPortalSiblingIdentityService>().HasVerifiedGuardianIdentityAsync(user)) continue;
			}
			if (!await portalRoleManager.RoleExistsAsync(type == "student" ? "Student" : "Parent")) continue;
			await ProvisionPortalAccountAsync(item.Id, new() { AccountType = type, Relationship = relationship,
				Email = email.Trim(), RelationshipVerified = true, LinkExistingGuardian = user != null, ConcurrencyStamp = item.ConcurrencyStamp });
		}
	}

	private async Task QueueAutomaticCredentialEmailsAsync(StudentOnboardingCase item)
	{
		var settings = await configurationManager.GetAsync(item.AcademicYearId);
		if (settings == null || OnboardingRuleConfiguration.Text(settings.Readiness(item), "credentialProvisioningMode", "manual") != "automatic" ||
			!await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit)) return;
		var emails = LazyServiceProvider.LazyGetRequiredService<StudentCredentialEmailAppService>();
		await emails.QueueAsync(item.Id, "student");
		await emails.QueueAsync(item.Id, "guardian");
	}
}
