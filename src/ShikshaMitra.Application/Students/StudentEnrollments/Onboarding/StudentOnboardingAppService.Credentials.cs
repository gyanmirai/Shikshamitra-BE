using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Portal;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public partial class StudentOnboardingAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	public async Task<StudentCredentialsWorkspaceDto> GetCredentialsAsync(Guid id)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View);
		EnsureCredentialsActor();
		return await MapCredentialsAsync(await GetCaseAsync(id));
	}

	/// <summary>Authorize the saved owner, prepare the account and persist email intent atomically.</summary>
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit)]
	[UnitOfWork(true)]
	public async Task<StudentCredentialsWorkspaceDto> SendCredentialsAsync(Guid id, SendStudentCredentialsDto input)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit);
		EnsureCredentialsActor();
		var validation = await new SendStudentCredentialsDtoValidator().ValidateAsync(input);
		if (!validation.IsValid) throw PortalInvalid(validation.Errors[0].ErrorMessage);
		var item = await GetCaseAsync(id);
		if (ShikshaMitra.Students.Records.StudentRecordAdmissionReturnService.ReturnTransitionId(item).HasValue)
			await LazyServiceProvider.LazyGetRequiredService<ShikshaMitra.Students.Records.StudentRecordAdmissionReturnService>().ValidateOnboardingAsync(item, false);
		var owner = (await LazyServiceProvider.LazyGetRequiredService<StudentCredentialsRecipientReader>().GetAsync(item))
			.SingleOrDefault(value => value.AccountType == input.AccountType && value.Relationship == input.Relationship)
			?? throw PortalInvalid("Choose a saved Student or parent account owner.");
		if (owner.BlockedReason != null) throw PortalInvalid(owner.BlockedReason);
		if (!StudentCredentialsRecipientReader.SameEmail(owner.Email, input.ExpectedEmail))
			throw PortalInvalid("The saved email changed. Refresh the recipient before sending credentials.");
		var resolver = LazyServiceProvider.LazyGetRequiredService<StudentOnboardingCredentialEligibilityResolver>();
		var state = await resolver.ResolveSendPreparationAsync(id, input.AccountType);
		EnsureCredentialsPreparation(state, owner);
		var requests = LazyServiceProvider.LazyGetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>();
		var existing = state.LinkId.HasValue ? await requests.FindAsync(row => row.CaseId == id &&
			row.AccountType == input.AccountType && row.LinkId == state.LinkId.Value) : null;
		// A repeated, still-authorized exact intent is a read, even if its successful first
		// request changed the case stamp. Never replay account creation or resend transport.
		if ((existing?.Status is "Pending" or "Processing") && StudentCredentialsRecipientReader.SameEmail(existing.Email, owner.Email))
		{
			if (existing.RequiresSavedOwnerValidation()) return await MapCredentialsAsync(item);
			if (existing.Status == "Processing") throw PortalInvalid("A credential email is being processed. Refresh its status before sending again.");
			// An unclaimed automatic intent may become this authorized manual intent,
			// using the current case stamp and the outbox row's optimistic concurrency.
		}
		EnsureConcurrency(item, input.ConcurrencyStamp);
		if (!state.LinkId.HasValue)
		{
			await AuthorizationService.CheckAsync(IdentityPermissions.Users.Create);
			await ProvisionPortalAccountCoreAsync(id, new() { AccountType = input.AccountType,
				Relationship = input.Relationship, Email = owner.Email!, ConcurrencyStamp = input.ConcurrencyStamp,
				LinkExistingGuardian = input.AccountType == "guardian" }, credentialsRequested: true);
		}
		else
		{
			// A reviewed existing link can outlive a pending handoff (for example a
			// returning Student). Reconcile it before deciding completion-time delivery.
			await RefreshProvisioningAsync(item);
		}
		item = await GetCaseAsync(id);
		await LazyServiceProvider.LazyGetRequiredService<StudentCredentialEmailAppService>()
			.RequestPreparedAsync(id, input.AccountType, item.ConcurrencyStamp);
		await AddEventAsync(id, "CredentialEmailRequested",
			$"Staff requested credentials for the saved {input.AccountType} owner ({input.Relationship}). Delivery follows the saved school timing.");
		await CurrentUnitOfWork!.SaveChangesAsync();
		return await MapCredentialsAsync(await GetCaseAsync(id));
	}

	private async Task<StudentCredentialsWorkspaceDto> MapCredentialsAsync(StudentOnboardingCase item)
	{
		var owners = await LazyServiceProvider.LazyGetRequiredService<StudentCredentialsRecipientReader>().GetAsync(item);
		var requests = await LazyServiceProvider.LazyGetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>()
			.GetListAsync(row => row.CaseId == item.Id);
		var resolver = LazyServiceProvider.LazyGetRequiredService<StudentOnboardingCredentialEligibilityResolver>();
		var states = new Dictionary<string, StudentCredentialEmailEligibility>();
		foreach (var type in owners.Select(owner => owner.AccountType).Distinct())
			states[type] = await resolver.ResolveSendPreparationAsync(item.Id, type);
		var canEdit = await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit);
		var canCreate = await AuthorizationService.IsGrantedAsync(IdentityPermissions.Users.Create);
		var canUpdate = await AuthorizationService.IsGrantedAsync(IdentityPermissions.Users.Update);
		foreach (var owner in owners)
		{
			var state = states[owner.AccountType];
			owner.BlockedReason ??= PreparationBlockedReason(state, owner);
			if (owner.BlockedReason == null && !canEdit) owner.BlockedReason = "You do not have permission to send credentials.";
			if (owner.BlockedReason == null && !state.LinkId.HasValue && !canCreate)
				owner.BlockedReason = "An authorized identity administrator must prepare this account.";
			if (owner.BlockedReason == null && !state.LinkId.HasValue &&
				!await portalRoleManager.RoleExistsAsync(owner.AccountType == "student" ? "Student" : "Parent"))
				owner.BlockedReason = "The school's portal role needs to be configured by an identity administrator.";
			if (owner.BlockedReason == null && !state.LinkId.HasValue && owner.AccountType == "guardian")
			{
				var identity = await portalUserManager.FindByEmailAsync(owner.Email!);
				if (identity != null)
				{
					if (!canUpdate) owner.BlockedReason = "An authorized identity administrator must link this existing account.";
					else
					{
						var roles = await portalUserManager.GetRolesAsync(identity);
						if (!identity.IsActive || roles.Count != 1 || roles[0] != "Parent" ||
							!await LazyServiceProvider.LazyGetRequiredService<StudentPortalSiblingIdentityService>().HasVerifiedGuardianIdentityAsync(identity))
							owner.BlockedReason = "The saved email cannot be linked automatically. Ask an identity administrator to review it.";
					}
				}
			}
			var row = requests.FirstOrDefault(request => request.AccountType == owner.AccountType && request.LinkId == state.LinkId &&
				StudentCredentialsRecipientReader.SameEmail(request.Email, owner.Email));
			if (state.LinkId.HasValue && state.Relationship == owner.Relationship)
				owner.Delivery = StudentCredentialEmailAppService.Map(owner.AccountType, state, row);
			if (owner.BlockedReason == null && (row?.Status is "Pending" or "Processing"))
				owner.BlockedReason = state.WaitingForCompletion ? "Credentials are queued for onboarding completion." : "Credentials are already queued.";
			else if (owner.BlockedReason == null && row != null && !row.CanRequest(DateTime.UtcNow))
				owner.BlockedReason = "Wait two minutes before sending credentials again.";
			owner.CanSend = owner.BlockedReason == null;
		}
		return new() { CaseId = item.Id, ConcurrencyStamp = item.ConcurrencyStamp, Recipients = owners };
	}

	private void EnsureCredentialsActor()
	{
		if (!CurrentTenant.Id.HasValue || !CurrentUser.Id.HasValue) throw new AbpAuthorizationException();
	}
	private static void EnsureCredentialsPreparation(StudentCredentialEmailEligibility state, StudentCredentialsRecipientDto owner)
	{
		var reason = PreparationBlockedReason(state, owner);
		if (reason != null) throw PortalInvalid(reason);
	}
	private static string? PreparationBlockedReason(StudentCredentialEmailEligibility state, StudentCredentialsRecipientDto owner)
	{
		if (!state.EmailEnabled || !state.IsReady && !state.WaitingForCompletion)
			return state.BlockedReason == StudentOnboardingCredentialEligibilityResolver.AccountRequired
				? "An identity administrator must review this account before credentials can be sent."
				: state.BlockedReason ?? "Complete the student's onboarding requirements before sending credentials.";
		if (state.LinkId.HasValue && (state.Relationship != owner.Relationship ||
			!StudentCredentialsRecipientReader.SameEmail(state.Email, owner.Email)))
			return "A different account owner is already linked. Ask an identity administrator to review it.";
		return null;
	}
}
