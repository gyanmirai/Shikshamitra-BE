using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Portal;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Identity;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public partial class StudentOnboardingAppService
{
	private StudentSiblingParentService SiblingParents => LazyServiceProvider.LazyGetRequiredService<StudentSiblingParentService>();

	private async Task<StudentSiblingParentContextDto> ReadSiblingParentContextAsync(StudentOnboardingCase item)
	{
		var context = await SiblingParents.GetAsync(item);
		context.CanLink = await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit) &&
			await AuthorizationService.IsGrantedAsync(IdentityPermissions.Users.Update);
		var rules = await ReadSavedRulesAsync(item);
		if (context.Enabled && (!rules.HasValue || !StudentPortalAccountRules.IsEnabled(rules.Value, "guardian")))
		{
			context.Candidates = [];
			context.RequiresSelection = false;
			context.Status = "NotRequired";
		}
		return context;
	}

	private async Task PrepareSiblingParentSelectionAsync(StudentOnboardingCase item, StudentSiblingParentSelectionDto? input,
		StudentGuardianAccessSelectionDto? guardianAccess = null)
	{
		if (guardianAccess != null)
		{
			if (input != null) throw PortalInvalid("Choose one adult app-access recipient for this case.");
			await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit);
			await AuthorizationService.CheckAsync(IdentityPermissions.Users.Create);
			EnsureCredentialsActor();
			var accessRules = await ReadSavedRulesAsync(item);
			if (!accessRules.HasValue || !StudentPortalAccountRules.IsEnabled(accessRules.Value, "guardian"))
				throw PortalInvalid("Adult app access is not enabled by this case's saved school settings.");
			await SiblingParents.PrepareGuardianSelectionAsync(item, guardianAccess);
			await caseRepository.UpdateAsync(item, false);
			await AddEventAsync(item.Id, "GuardianAccessSelected", "Staff explicitly selected the saved guardian for this child's app access. Sibling parent accounts and main school contact remain independent.");
			return;
		}
		if (input != null && !(await new StudentSiblingParentSelectionDtoValidator().ValidateAsync(input)).IsValid)
			throw PortalInvalid("Choose a saved Father or Mother account before activating the student.");
		var context = await ReadSiblingParentContextAsync(item);
		if (context.Status == "NotRequired")
		{
			if (input != null) throw PortalInvalid("Parent app login is not enabled by the saved school settings.");
			return;
		}
		if (context.Candidates.Count > 0 && !context.IsLinked)
		{
			await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit);
			await AuthorizationService.CheckAsync(IdentityPermissions.Users.Update);
		}
		await SiblingParents.PrepareSelectionAsync(item, input);
	}

	private async Task LinkSiblingParentAsync(StudentOnboardingCase item)
	{
		if (!item.StudentEnrollmentId.HasValue || item.Status == StudentOnboardingStatus.Cancelled) return;
		var rules = await ReadSavedRulesAsync(item);
		if (!rules.HasValue || !StudentPortalAccountRules.IsEnabled(rules.Value, "guardian")) return;
		var selected = await SiblingParents.ResolveSelectedAsync(item);
		if (selected == null) return;
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit);
		await AuthorizationService.CheckAsync(IdentityPermissions.Users.Update);
		EnsureCredentialsActor();
		var link = await portalLinkRepository.FindAsync(x => x.StudentId == item.StudentId && x.AccountType == "guardian");
		if (link != null && (link.UserId != selected.UserId || link.Relationship != selected.Relationship || link.VerifiedByUserId == Guid.Empty))
			throw PortalInvalid("A different parent account is already linked. Ask an administrator to review it.");
		if (link == null)
		{
			// Fence existing-account linking against an owner-verified email change.
			// This advances only optimistic concurrency, never password/security state.
			var parentUser = await portalUserManager.FindByIdAsync(selected.UserId.ToString()) ?? throw PortalInvalid("The parent account is unavailable.");
			if (!StudentPortalProfileEmail.Same(parentUser.Email, selected.Email) || !(await portalUserManager.UpdateAsync(parentUser)).Succeeded)
				throw PortalInvalid("The parent account changed. Refresh App Login before linking.");
			link = new StudentPortalAccountLink(GuidGenerator.Create(), CurrentTenant.Id!.Value, item.StudentId,
				selected.UserId, "guardian", selected.Relationship, CurrentUser.Id!.Value);
			await portalLinkRepository.InsertAsync(link, false);
			await AddEventAsync(item.Id, "SiblingParentLinked", SiblingParents.UsesHistoricalParentSource(item)
				? "The confirmed existing parent account was reused from a historical admission. Existing relationships and credentials were preserved."
				: "The confirmed existing parent account was linked. No new credentials were requested.");
		}
		SiblingParents.MarkReused(item);
		var handoff = await handoffRepository.FindAsync(x => x.OnboardingCaseId == item.Id && x.ServiceKey == "guardian-account");
		if (handoff != null)
		{
			handoff.Acknowledge(DateTime.UtcNow);
			await handoffRepository.UpdateAsync(handoff, false);
		}
		await caseRepository.UpdateAsync(item, false);
		await CurrentUnitOfWork!.SaveChangesAsync();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit)]
	[UnitOfWork(true)]
	public async Task<StudentCredentialsWorkspaceDto> RetryParentSetupAsync(Guid id, RetryStudentParentSetupDto input)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit);
		EnsureCredentialsActor();
		if (string.IsNullOrWhiteSpace(input.ConcurrencyStamp) || input.ConcurrencyStamp.Length > 100) throw PortalInvalid("Refresh App Login before retrying parent setup.");
		var item = await GetCaseAsync(id);
		EnsureConcurrency(item, input.ConcurrencyStamp);
		var original = await SiblingParents.FindFailedSetupAsync(item) ?? throw PortalInvalid("There is no failed parent setup to retry.");
		// The sibling's scope must never authorize a request for another child's case.
		var source = await GetCaseAsync(original.CaseId);
		await LazyServiceProvider.LazyGetRequiredService<StudentCredentialEmailAppService>()
			.RetryOriginalParentSetupAsync(original, source.ConcurrencyStamp);
		await AddEventAsync(item.Id, "ParentSetupRetried", "Staff retried the original parent account setup. No sibling credentials were created.");
		await CurrentUnitOfWork!.SaveChangesAsync();
		return await MapCredentialsAsync(item);
	}

	private async Task<bool> CanRetryParentSetupAsync(StudentOnboardingCase item)
	{
		if (!await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit)) return false;
		var request = await SiblingParents.FindFailedSetupAsync(item);
		if (request == null || !request.CanRequest(DateTime.UtcNow)) return false;
		try { await GetCaseAsync(request.CaseId); return true; }
		catch (AbpAuthorizationException) { return false; }
		catch (EntityNotFoundException) { return false; }
	}

	/// <summary>Attach an existing confirmed parent after enrollment without replaying student setup.</summary>
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit)]
	[Authorize(IdentityPermissions.Users.Update)]
	[UnitOfWork(true)]
	public async Task<StudentCredentialsWorkspaceDto> LinkParentAsync(Guid id, LinkStudentSiblingParentDto input)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit);
		await AuthorizationService.CheckAsync(IdentityPermissions.Users.Update);
		EnsureCredentialsActor();
		if (string.IsNullOrWhiteSpace(input.ConcurrencyStamp) || input.ConcurrencyStamp.Length > 100)
			throw PortalInvalid("Refresh App Login before linking the parent.");
		var item = await GetCaseAsync(id);
		EnsureConcurrency(item, input.ConcurrencyStamp);
		if (!item.StudentEnrollmentId.HasValue || item.Status == StudentOnboardingStatus.Cancelled)
			throw PortalInvalid("Save and activate the student before linking the parent.");
		if (ShikshaMitra.Students.Records.StudentRecordAdmissionReturnService.ReturnTransitionId(item).HasValue)
			await LazyServiceProvider.LazyGetRequiredService<ShikshaMitra.Students.Records.StudentRecordAdmissionReturnService>().ValidateOnboardingAsync(item, false);
		var context = await ReadSiblingParentContextAsync(item);
		if (!context.Enabled || !context.FamilyConfirmed || context.Status == "NotRequired" || context.Candidates.Count == 0)
			throw PortalInvalid("Confirm an available Father or Mother through Add sibling in Admissions first.");
		await PrepareSiblingParentSelectionAsync(item, input.SiblingParentSelection);
		await LinkSiblingParentAsync(item);
		await RefreshProvisioningAsync(item);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return await MapCredentialsAsync(item);
	}
}
