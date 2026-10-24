using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

/// <summary>Reads confirmed family provenance and current parent identities; never creates users or sends email.</summary>
[RemoteService(false)]
public class StudentSiblingParentService(IRepository<AdmissionApplicant, Guid> applicants,
	IRepository<StudentOnboardingCase, Guid> cases, IStudentRepository students,
	IRepository<StudentPortalAccountLink, Guid> links, IdentityUserManager users,
	IRepository<StudentCredentialEmailRequest, Guid> requests,
	StudentCredentialsRecipientReader recipients) : ApplicationService, ITransientDependency
{
	private const string SelectionKey = "SiblingParentActivationSelection";
	private const string ReuseKey = "SiblingParentAccountReused";
	private const string SourceRelationshipKey = "SiblingParentActivationSourceRelationship";
	public async Task<bool> IsParentFlowAsync(StudentOnboardingCase item)
	{
		var applicant = await applicants.FindAsync(item.ApplicantId);
		return applicant != null && applicant.TenantId == CurrentTenant.Id && AdmissionSiblingParentSelection.IsParentFlow(applicant);
	}

	public async Task<StudentSiblingParentContextDto> GetAsync(StudentOnboardingCase item) => (await ReadAsync(item)).Context;

	private sealed class ParentRead
	{
		public StudentSiblingParentContextDto Context { get; } = new();
		public Dictionary<string, StudentCredentialEmailRequest?> InitialRequests { get; } = [];
		public Dictionary<string, string> SourceRelationships { get; } = [];
	}

	private async Task<ParentRead> ReadAsync(StudentOnboardingCase item)
	{
		var read = new ParentRead();
		var result = read.Context;
		var applicant = await applicants.FindAsync(item.ApplicantId);
		if (!CurrentTenant.Id.HasValue || item.TenantId != CurrentTenant.Id || applicant == null ||
			applicant.TenantId != CurrentTenant.Id || !AdmissionSiblingParentSelection.IsParentFlow(applicant)) return read;
		result.Enabled = true;
		var confirmation = AdmissionSiblingParentSelection.ReadConfirmation(applicant);
		if (confirmation == null) return read;
		result.FamilyConfirmed = true;
		var source = await applicants.FindAsync(confirmation.SourceApplicantId);
		if (source == null || source.TenantId != CurrentTenant.Id || source.FamilyId != applicant.FamilyId ||
			confirmation.ConfirmedBy is null || confirmation.ConfirmedBy == Guid.Empty)
			return Block(read, "Review the confirmed parent family before linking app access.");
		var confirmedSourceStudentId = source.ExistingStudentId ?? source.ReservedStudentId;
		var familyStudents = await AsyncExecuter.ToListAsync((await applicants.GetQueryableAsync())
			.Where(x => x.TenantId == CurrentTenant.Id && x.FamilyId == applicant.FamilyId)
			.Select(x => new { x.ReservedStudentId, x.ExistingStudentId }));
		var studentIds = familyStudents.SelectMany(x => new[] { x.ReservedStudentId, x.ExistingStudentId })
			.Where(x => x.HasValue && x != item.StudentId).Select(x => x!.Value).Distinct().ToArray();
		var familyLinks = await links.GetListAsync(x => x.TenantId == CurrentTenant.Id &&
			Enumerable.Contains(studentIds, x.StudentId) && x.AccountType == "guardian" &&
			(x.Relationship == "father" || x.Relationship == "mother" ||
			 x.Relationship == "guardian" && x.StudentId == confirmedSourceStudentId) && x.VerifiedByUserId != Guid.Empty);
		var profileIds = familyLinks.Select(x => x.StudentId).Distinct().ToArray();
		var profiles = await AsyncExecuter.ToListAsync((await students.GetQueryableWithoutAutoIncludesAsync())
			.Where(x => x.TenantId == CurrentTenant.Id && Enumerable.Contains(profileIds, x.Id))
			.Select(x => new { x.Id, FatherEmail = x.FamilyDetail == null || x.FamilyDetail.FatherDetails == null ? null : x.FamilyDetail.FatherDetails.Email,
				MotherEmail = x.FamilyDetail == null || x.FamilyDetail.MotherDetails == null ? null : x.FamilyDetail.MotherDetails.Email,
				LegacyParentEmail = x.FamilyDetail == null || x.FamilyDetail.GuardianDetails == null ? null : x.FamilyDetail.GuardianDetails.Email }));
		var candidates = new List<StudentSiblingParentCandidateDto>();
		foreach (var owner in (await recipients.GetAsync(item)).Where(x => x.AccountType == "guardian" &&
			AdmissionSiblingParentSelection.IsRelationship(x.Relationship) && x.BlockedReason == null))
		{
			var user = await users.FindByEmailAsync(owner.Email!);
			if (user == null) continue;
			var roles = await users.GetRolesAsync(user);
			if (user.TenantId != CurrentTenant.Id || !user.IsActive || roles.Count != 1 || roles[0] != "Parent") continue;
			var verified = familyLinks.Where(link => link.UserId == user.Id && profiles.Any(profile => profile.Id == link.StudentId &&
				(link.Relationship == owner.Relationship && StudentCredentialsRecipientReader.SameEmail(
					link.Relationship == "father" ? profile.FatherEmail : profile.MotherEmail, user.Email) ||
				 // A historical generic link is not inferred to be Father/Mother: the exact
				 // confirmed source and staff's new relationship attestation are mandatory.
				 link.Relationship == "guardian" && link.StudentId == confirmedSourceStudentId &&
				 confirmation.Relationship == owner.Relationship &&
				 StudentCredentialsRecipientReader.SameEmail(confirmation.Email, owner.Email) &&
				 StudentCredentialsRecipientReader.SameEmail(profile.LegacyParentEmail, user.Email))))
				.OrderBy(link => link.Relationship == "guardian").ToList();
			if (verified.Count == 0) continue;
			var verifiedIds = verified.Select(x => x.Id).ToArray();
			var initial = (await requests.GetListAsync(row => row.TenantId == CurrentTenant.Id && row.UserId == user.Id &&
				Enumerable.Contains(verifiedIds, row.LinkId) && row.AccountType == "guardian" &&
				(row.Status == "Pending" || row.Status == "Processing" || row.Status == "Sent" || row.Status == "Failed")))
				.Where(row => StudentCredentialsRecipientReader.SameEmail(row.Email, user.Email) &&
					verified.Any(link => link.Id == row.LinkId && link.StudentId == row.StudentId))
				.OrderByDescending(row => row.CreationTime).FirstOrDefault();
			if (!user.EmailConfirmed && initial == null) continue;
			read.InitialRequests[owner.Relationship] = initial;
			read.SourceRelationships[owner.Relationship] = verified[0].Relationship;
			candidates.Add(new() { Relationship = owner.Relationship, Name = owner.Name, Email = owner.Email!, UserId = user.Id,
				SetupStatus = user.EmailConfirmed && !user.ShouldChangePasswordOnNextLogin && await users.HasPasswordAsync(user) ? "Ready"
					: initial?.Status == "Failed" ? "Failed" : "Pending" });
		}
		result.Candidates = candidates;
		result.RequiresSelection = candidates.Count > 1;
		var selected = ReadSelection(item);
		if (selected != null)
		{
			if (!candidates.Any(candidate => Same(candidate, selected))) return Block(read, "The reviewed parent account changed. Ask an administrator to review App Login.");
			result.SelectedRelationship = selected.Relationship;
		}
		var linked = await links.FindAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == item.StudentId && x.AccountType == "guardian");
		if (linked != null)
		{
			var candidate = candidates.Find(x => x.UserId == linked.UserId && x.Relationship == linked.Relationship);
			if (candidate == null || linked.VerifiedByUserId == Guid.Empty || selected != null && !Same(candidate, selected))
				return Block(read, "The linked parent differs from the confirmed family. Ask an administrator to review it.");
			result.IsLinked = true; result.LinkedRelationship = candidate.Relationship;
			result.Status = candidate.SetupStatus == "Ready" ? "Linked" : candidate.SetupStatus == "Failed" ? "SetupFailed" : "SetupPending";
			return read;
		}
		result.Status = candidates.Count > 1 ? "SelectParent" : candidates.Count == 1 ? "Available" : "NewParent";
		return read;
	}

	public async Task PrepareSelectionAsync(StudentOnboardingCase item, StudentSiblingParentSelectionDto? input)
	{
		var read = await ReadAsync(item);
		var context = read.Context;
		if (context.BlockedReason != null) throw Invalid(context.BlockedReason);
		if (!context.Enabled || !context.FamilyConfirmed)
		{
			if (input != null) throw Invalid("Refresh the confirmed parent family before choosing app access.");
			return;
		}
		if (context.IsLinked)
		{
			if (input != null && !context.Candidates.Any(x => x.Relationship == context.LinkedRelationship && Same(x, input)))
				throw Invalid("A different parent account is already linked. Review it before continuing.");
			return;
		}
		var saved = ReadSelection(item);
		if (saved != null && input != null && !Same(saved, input)) throw Invalid("The reviewed parent selection changed. Refresh app login.");
		input ??= saved;
		if (input == null && context.RequiresSelection) throw Invalid("Choose Father or Mother in App Login before activating this student.");
		var candidate = input == null ? context.Candidates.SingleOrDefault() : context.Candidates.SingleOrDefault(x => Same(x, input));
		if (input != null && candidate == null) throw Invalid("The parent account changed or is unavailable. Refresh App Login before activating.");
		if (candidate == null) return;
		item.SetProperty(SelectionKey, JsonSerializer.Serialize(new StudentSiblingParentSelectionDto {
			Relationship = candidate.Relationship, ExpectedEmail = candidate.Email, ExpectedUserId = candidate.UserId }));
		item.SetProperty(SourceRelationshipKey, read.SourceRelationships[candidate.Relationship]);
	}

	public async Task<StudentSiblingParentCandidateDto?> ResolveSelectedAsync(StudentOnboardingCase item)
	{
		var selected = ReadSelection(item);
		if (selected == null) return null;
		var context = await GetAsync(item);
		if (context.BlockedReason != null) throw Invalid(context.BlockedReason);
		return context.Candidates.SingleOrDefault(x => Same(x, selected)) ?? throw Invalid("The reviewed parent account is no longer eligible. Review App Login.");
	}

	/// <summary>Resolve only the original account-setup intent, never a new sibling delivery.</summary>
	public async Task<StudentCredentialEmailRequest?> FindFailedSetupAsync(StudentOnboardingCase item)
	{
		var read = await ReadAsync(item);
		var context = read.Context;
		if (!context.IsLinked || context.Status != "SetupFailed" || context.BlockedReason != null) return null;
		var original = read.InitialRequests.GetValueOrDefault(context.LinkedRelationship!);
		return original?.Status == "Failed" ? original : null;
	}
	public void MarkReused(StudentOnboardingCase item) => item.SetProperty(ReuseKey, true);
	internal static void RefreshEmailSelection(StudentOnboardingCase item, Guid userId, string previousEmail, string email)
	{
		var selection = ReadSelection(item);
		if (selection?.ExpectedUserId != userId || !StudentPortalProfileEmail.Same(selection.ExpectedEmail, previousEmail)) return;
		selection.ExpectedEmail = email;
		item.SetProperty(SelectionKey, JsonSerializer.Serialize(selection));
	}
	public bool UsesHistoricalParentSource(StudentOnboardingCase item) => item.GetProperty<string>(SourceRelationshipKey) == "guardian";
	public bool WasReused(StudentOnboardingCase item) => item.TenantId == CurrentTenant.Id && item.GetProperty<bool>(ReuseKey);
	public async Task<bool> IsReusedAsync(Guid caseId, string accountType)
	{
		if (accountType != "guardian") return false;
		var item = await cases.FindAsync(caseId);
		return item != null && WasReused(item) && await IsParentFlowAsync(item);
	}
	public async Task<bool> IsManualParentCaseAsync(Guid caseId, string accountType)
	{
		if (accountType != "guardian") return false;
		var item = await cases.FindAsync(caseId);
		return item != null && item.TenantId == CurrentTenant.Id && await IsParentFlowAsync(item);
	}
	private static ParentRead Block(ParentRead read, string reason)
	{
		read.Context.Status = "ReviewRequired"; read.Context.BlockedReason = reason; return read;
	}
	private static StudentSiblingParentSelectionDto? ReadSelection(StudentOnboardingCase item)
	{
		try { return JsonSerializer.Deserialize<StudentSiblingParentSelectionDto>(item.GetProperty<string>(SelectionKey) ?? "null"); }
		catch (JsonException) { throw Invalid("The saved parent selection requires review."); }
	}
	private static bool Same(StudentSiblingParentCandidateDto candidate, StudentSiblingParentSelectionDto input) =>
		candidate.Relationship == input.Relationship && candidate.UserId == input.ExpectedUserId && StudentCredentialsRecipientReader.SameEmail(candidate.Email, input.ExpectedEmail);
	private static bool Same(StudentSiblingParentSelectionDto first, StudentSiblingParentSelectionDto second) =>
		first.Relationship == second.Relationship && first.ExpectedUserId == second.ExpectedUserId && StudentCredentialsRecipientReader.SameEmail(first.ExpectedEmail, second.ExpectedEmail);
	private static BusinessException Invalid(string reason) => new BusinessException(StudentOnboardingErrorCodes.PortalAccountInvalid).WithData("Reason", reason);
}
