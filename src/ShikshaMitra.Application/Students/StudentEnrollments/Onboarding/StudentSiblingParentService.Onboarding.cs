using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.Records;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public partial class StudentSiblingParentService
{
	private const string OnboardingConfirmationKey = "OnboardingExistingParentConfirmedBy";
	internal static bool HasOnboardingConfirmation(StudentOnboardingCase item) => item.GetProperty<Guid?>(OnboardingConfirmationKey) is Guid actor && actor != Guid.Empty;

	private async Task<ParentRead> ReadOnboardingParentsAsync(StudentOnboardingCase item, bool validateHelp = false, string? proposedRelationship = null, string? proposedEmail = null)
	{
		var read = new ParentRead(); var context = read.Context;
		if (!CurrentTenant.Id.HasValue || item.TenantId != CurrentTenant.Id) return read;
		context.Enabled = true;
		if (await LazyServiceProvider.LazyGetRequiredService<IRepository<StudentGuardianAccessReview, Guid>>().AnyAsync(x => x.StudentId == item.StudentId && x.Status == "Pending"))
			return Block(read, "An adult app-access review is pending. Complete the reviewed access workflow before linking or sending sign-in help.");
		var current = await links.FindAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == item.StudentId && x.AccountType == "guardian");
		var selected = ReadSelection(item);
		var owners = await recipients.GetAsync(item);
		if (current != null && !HasOnboardingConfirmation(item) && !validateHelp) return read;
		if (ReadGuardianSelection(item) != null && !HasOnboardingConfirmation(item) && !validateHelp) { context.GuardianAccessSelected = true; context.Status = "NotRequired"; return read; }
		var candidates = new List<StudentSiblingParentCandidateDto>();
		foreach (var owner in owners.Where(x => x.AccountType == "guardian" && (x.BlockedReason == null || x.Relationship == proposedRelationship)))
		{
			var email = owner.Relationship == proposedRelationship ? proposedEmail : owner.Email;
			if (string.IsNullOrWhiteSpace(email)) continue;
			var user = await users.FindByEmailAsync(email);
			if (user == null || user.TenantId != CurrentTenant.Id || !user.IsActive || await users.IsLockedOutAsync(user)) continue;
			var roles = await users.GetRolesAsync(user);
			if (roles.Count != 1 || roles[0] != "Parent") continue;
			var knownLinks = await links.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId != item.StudentId && x.UserId == user.Id && x.AccountType == "guardian" && x.VerifiedByUserId != Guid.Empty);
			var verified = new List<StudentPortalAccountLink>();
			foreach (var source in knownLinks)
			{
				var profile = await students.GetCredentialsProfileAsync(source.StudentId);
				if (profile == null || profile.TenantId != CurrentTenant.Id || !StudentPortalProfileEmail.Same(StudentPortalProfileEmail.Read(profile, source.Relationship), user.Email)) continue;
				var scoped = await LazyServiceProvider.LazyGetRequiredService<StudentScopeAccessService>().ApplyCurrentUserScopeAsync(await students.GetQueryableWithoutAutoIncludesAsync());
				if (!await AsyncExecuter.AnyAsync(scoped.Where(x => x.Id == source.StudentId))) continue;
				if (await LazyServiceProvider.LazyGetRequiredService<IRepository<StudentGuardianAccessReview, Guid>>().AnyAsync(x => x.StudentId == source.StudentId && x.Status == "Pending")) continue;
				verified.Add(source);
			}
			if (verified.Count == 0) continue;
			var verifiedIds = verified.Select(x => x.Id).ToArray();
			var initial = (await requests.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.UserId == user.Id && Enumerable.Contains(verifiedIds, x.LinkId) && x.AccountType == "guardian"))
				.Where(x => !x.IsExistingParentHelp() && (x.Status is "Pending" or "Processing" or "Sent" or "Failed") && StudentPortalProfileEmail.Same(x.Email, user.Email) && verified.Any(link => link.Id == x.LinkId && link.StudentId == x.StudentId))
				.OrderByDescending(x => x.CreationTime).FirstOrDefault();
			if (!user.EmailConfirmed && initial == null) continue;
			read.InitialRequests[owner.Relationship] = initial;
			read.SourceRelationships[owner.Relationship] = verified[0].Relationship;
			candidates.Add(new() { Relationship = owner.Relationship, Email = email, Name = owner.Name, UserId = user.Id,
				SetupStatus = user.EmailConfirmed && !user.ShouldChangePasswordOnNextLogin && await users.HasPasswordAsync(user) ? "Ready" : initial?.Status == "Failed" ? "Failed" : "Pending" });
		}
		context.Candidates = candidates;
		context.RequiresOnboardingConfirmation = !HasOnboardingConfirmation(item) && candidates.Count > 0;
		context.RequiresSelection = candidates.Count > 1;
		context.Status = candidates.Count > 0 ? "Available" : "NewParent";
		if (!HasOnboardingConfirmation(item) && !validateHelp) return read;
		var candidate = selected == null ? null : candidates.SingleOrDefault(x => Same(x, selected));
		if (candidate == null || current != null && (current.VerifiedByUserId == Guid.Empty || current.UserId != candidate.UserId || current.Relationship != candidate.Relationship))
			return Block(read, "The reviewed existing adult or linked account changed. Ask authorized staff to review App Login.");
		context.SelectedRelationship = candidate.Relationship;
		if (current != null)
		{
			context.Candidates = [candidate]; context.RequiresSelection = false; context.IsLinked = true; context.LinkedRelationship = candidate.Relationship;
			context.GuardianAccessSelected = candidate.Relationship == "guardian";
			context.Status = candidate.SetupStatus == "Ready" ? "Linked" : candidate.SetupStatus == "Failed" ? "SetupFailed" : "SetupPending";
		}
		return read;
	}

	public async Task<bool> CanSendExistingParentHelpAsync(Guid caseId)
	{
		var item = await cases.FindAsync(caseId);
		if (item == null || item.TenantId != CurrentTenant.Id || !WasReused(item)) return false;
		var read = await ReadOnboardingParentsAsync(item, validateHelp: true);
		return read.Context.IsLinked && read.Context.BlockedReason == null;
	}
	internal async Task<bool> CanSaveExistingParentEmailAsync(StudentOnboardingCase item, string relationship, string email)
	{
		var read = await ReadOnboardingParentsAsync(item, proposedRelationship: relationship, proposedEmail: email);
		return read.Context.BlockedReason == null && read.Context.Candidates.Any(x => x.Relationship == relationship && StudentPortalProfileEmail.Same(x.Email, email));
	}

	internal async Task PrepareOnboardingSelectionAsync(StudentOnboardingCase item, StudentSiblingParentSelectionDto? input)
	{
		if (!CurrentUser.Id.HasValue || input == null || !(await new StudentSiblingParentSelectionDtoValidator().ValidateAsync(input)).IsValid)
			throw Invalid("Confirm the current saved adult and existing Parent account before linking.");
		var read = await ReadOnboardingParentsAsync(item);
		if (read.Context.BlockedReason != null) throw Invalid(read.Context.BlockedReason);
		var candidate = read.Context.Candidates.SingleOrDefault(x => Same(x, input)) ?? throw Invalid("The saved email is conflicting, inactive, restricted or has no accessible verified Parent association. Refresh App Login or ask authorized staff to review it.");
		var current = await links.FindAsync(x => x.StudentId == item.StudentId && x.AccountType == "guardian");
		if (current != null && (!HasOnboardingConfirmation(item) || current.UserId != candidate.UserId || current.Relationship != candidate.Relationship || current.VerifiedByUserId == Guid.Empty))
			throw Invalid("A different adult access choice is already linked. Use the reviewed access repair workflow.");
		if (ReadGuardianSelection(item) != null && !HasOnboardingConfirmation(item)) throw Invalid("Guardian app access is already selected. Use the reviewed access repair workflow.");
		var previous = ReadSelection(item);
		if (previous != null && !Same(previous, input)) throw Invalid("The reviewed adult selection changed. Use the reviewed access repair workflow.");
		item.SetProperty(SelectionKey, JsonSerializer.Serialize(input));
		item.SetProperty(OnboardingConfirmationKey, CurrentUser.Id.Value);
		item.SetProperty("OnboardingExistingParentConfirmedAt", Clock.Now);
		item.SetProperty(SourceRelationshipKey, read.SourceRelationships[candidate.Relationship]);
	}
}
