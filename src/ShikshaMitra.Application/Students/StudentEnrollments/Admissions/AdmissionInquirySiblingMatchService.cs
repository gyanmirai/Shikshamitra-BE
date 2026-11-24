using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Scoped sibling suggestions and membership reads; never parent/account attestation.</summary>
[RemoteService(false)]
public class AdmissionInquirySiblingMatchService(
	IRepository<AdmissionApplicant, Guid> applicants,
	AdmissionInquiryScopeAccessService inquiryScope,
	AdmissionApplicationScopeAccessService applicationScope,
	AdmissionDirectFamilyMatchService familyMatches) : ApplicationService, ITransientDependency
{
	internal async Task<HashSet<Guid>> FindPossibleInquiryIdsAsync(IReadOnlyCollection<AdmissionInquiry> rows)
	{
		if (rows.Count == 0) return [];
		var context = await ReadContextAsync(rows);
		return rows.Where(row => IsPending(row) && context.TargetApplicants.Any(target => target.Id == row.ApplicantId && !target.ExistingStudentId.HasValue) &&
			context.Contacts.TryGetValue(row.Id, out var contact) &&
			!context.VisibleApplicants.Any(applicant => applicant.FamilyId == row.FamilyId && applicant.Id != row.ApplicantId) &&
			context.Families.Any(family => family.FamilyId != row.FamilyId && Matches(contact, family) &&
				context.VisibleApplicants.Any(applicant => applicant.FamilyId == family.FamilyId && applicant.Id != row.ApplicantId &&
					!IsSameChild(row, context.TargetApplicants.Single(target => target.Id == row.ApplicantId), applicant, context.SourceInquiries))))
			.Select(row => row.Id).ToHashSet();
	}

	internal async Task<List<AdmissionInquiryDuplicateMatchDto>> ReadMatchesAsync(AdmissionInquiry inquiry)
	{
		var context = await ReadContextAsync([inquiry]);
		if (!context.Contacts.TryGetValue(inquiry.Id, out var contact)) return [];
		var linked = context.VisibleApplicants.Any(applicant => applicant.FamilyId == inquiry.FamilyId && applicant.Id != inquiry.ApplicantId);
		var families = context.Families.Where(family => linked ? family.FamilyId == inquiry.FamilyId :
			family.FamilyId != inquiry.FamilyId && Matches(contact, family)).ToDictionary(family => family.FamilyId);
		var target = context.TargetApplicants.Single(applicant => applicant.Id == inquiry.ApplicantId);
		var ids = context.VisibleApplicants.Where(applicant => applicant.Id != inquiry.ApplicantId && families.ContainsKey(applicant.FamilyId) &&
			(linked || !IsSameChild(inquiry, target, applicant, context.SourceInquiries)))
			.Select(applicant => applicant.Id).ToArray();
		if (ids.Length == 0) return [];
		var sourceInquiries = context.SourceInquiries;
		return context.VisibleApplicants.Where(applicant => Enumerable.Contains(ids, applicant.Id)).Select(applicant =>
		{
			var family = families[applicant.FamilyId];
			var source = sourceInquiries.Where(row => row.ApplicantId == applicant.Id)
				.OrderByDescending(row => row.AdmissionCycleId == inquiry.AdmissionCycleId).ThenByDescending(row => row.CreationTime).FirstOrDefault();
			// Confirmed review intentionally uses the same complete visible family-parent snapshot on either child.
			var parents = family.Parents.OrderBy(parent => parent.SourceApplicantId).ThenBy(parent => parent.Relationship).ToList();
			var parent = parents.FirstOrDefault(candidate => candidate.SourceApplicantId == applicant.Id) ?? parents.FirstOrDefault();
			return new AdmissionInquiryDuplicateMatchDto
			{
				ApplicantId = applicant.Id, FamilyId = applicant.FamilyId, ExistingInquiryId = source?.Id,
				ExistingInquiryNumber = source?.InquiryNumber, ExistingInquiryAdmissionCycleId = source?.AdmissionCycleId,
				ExistingInquiryMatchesMobile = source == null ? null : NormalizePhone(source.NormalizedPhone) == NormalizePhone(inquiry.NormalizedPhone),
				ApplicantName = source?.ApplicantName ?? string.Join(" ", new[] { applicant.FirstName, applicant.MiddleName, applicant.LastName }.Where(value => !string.IsNullOrWhiteSpace(value))),
				DateOfBirth = applicant.DateOfBirth, GuardianPhone = parent?.Mobile, GuardianEmail = parent?.Email,
				CanLinkFamily = !linked && IsPending(inquiry) && !context.TargetApplicants.Any(target => target.ExistingStudentId.HasValue),
				Parents = parents, MatchByMobile = parents.Any(candidate => contact.Phones.Contains(NormalizePhone(candidate.Mobile))),
				MatchByEmail = parents.Any(candidate => contact.Emails.Contains(NormalizeEmail(candidate.Email)))
			};
		}).OrderBy(match => match.ApplicantName).ThenBy(match => match.ApplicantId).ToList();
	}

	private async Task<Context> ReadContextAsync(IReadOnlyCollection<AdmissionInquiry> rows)
	{
		var ids = rows.Where(row => row.ApplicantId.HasValue).Select(row => row.ApplicantId!.Value).Distinct().ToArray();
		var targets = ids.Length == 0 ? [] : await applicants.GetListAsync(applicant => applicant.TenantId == CurrentTenant.Id &&
			Enumerable.Contains(ids, applicant.Id) && applicant.Status == AdmissionApplicantStatus.Active);
		var contacts = rows.Where(row => targets.Any(target => target.Id == row.ApplicantId && target.FamilyId == row.FamilyId))
			.ToDictionary(row => row.Id, row => ReadContacts(row, targets.Single(target => target.Id == row.ApplicantId)));
		if (contacts.Count == 0) return new(contacts, [], [], targets, []);
		var canReadApplications = await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View);
		var retained = rows.Where(row => row.FamilyId.HasValue).Select(row => row.FamilyId!.Value).Distinct().ToArray();
		var families = await familyMatches.FindInquiryMatchesBatchAsync(contacts.Values.SelectMany(contact => contact.Phones),
			contacts.Values.SelectMany(contact => contact.Emails), retained, canReadApplications);
		var familyIds = families.Select(family => family.FamilyId).ToArray();
		if (familyIds.Length == 0) return new(contacts, families, [], targets, []);
		var scopedInquiries = await inquiryScope.GetScopedQueryAsync();
		var scopedApplications = await applicationScope.GetScopedApplicationsAsync();
		if (!canReadApplications) scopedApplications = scopedApplications.Where(_ => false);
		var visibleQuery = (await applicants.GetQueryableAsync()).Where(applicant => applicant.TenantId == CurrentTenant.Id &&
			applicant.Status == AdmissionApplicantStatus.Active && Enumerable.Contains(familyIds, applicant.FamilyId) &&
			(scopedInquiries.Any(row => row.ApplicantId == applicant.Id) || scopedApplications.Any(row => row.ApplicantId == applicant.Id)));
		var visible = await AsyncExecuter.ToListAsync(await LazyServiceProvider.LazyGetRequiredService<AdmissionSiblingSourceVisibilityService>().ApplyAsync(visibleQuery));
		var visibleIds = visible.Select(applicant => applicant.Id).ToArray();
		var sourceInquiries = visibleIds.Length == 0 ? [] : await AsyncExecuter.ToListAsync(scopedInquiries.Where(source =>
			source.ApplicantId.HasValue && Enumerable.Contains(visibleIds, source.ApplicantId.Value)));
		return new(contacts, families, visible, targets, sourceInquiries);
	}

	private static Contact ReadContacts(AdmissionInquiry inquiry, AdmissionApplicant applicant)
	{
		var useGeneric = AdmissionPrimaryContactSelection.ReadRelationship(applicant) != "guardian";
		return new(
		new[] { useGeneric ? inquiry.NormalizedPhone : null, AdmissionStudentProfileMapping.Read(applicant, "Father.Mobile"), AdmissionStudentProfileMapping.Read(applicant, "Mother.Mobile") }
			.Select(NormalizePhone).Where(value => value.Length == 10).ToHashSet(),
		new[] { useGeneric ? inquiry.Email : null, AdmissionStudentProfileMapping.Read(applicant, "Father.Email"), AdmissionStudentProfileMapping.Read(applicant, "Mother.Email") }
			.Select(NormalizeEmail).Where(value => value.Length > 0).ToHashSet());
	}
	private static bool Matches(Contact contact, AdmissionDirectFamilyMatchDto family) => family.Parents.Any(parent =>
		contact.Phones.Contains(NormalizePhone(parent.Mobile)) || contact.Emails.Contains(NormalizeEmail(parent.Email)));
	private static bool IsPending(AdmissionInquiry inquiry) => inquiry.DuplicateOfId == null &&
		inquiry.Status is AdmissionInquiryStatus.New or AdmissionInquiryStatus.Contacted or AdmissionInquiryStatus.FollowUp;
	private static bool IsSameChild(AdmissionInquiry inquiry, AdmissionApplicant target, AdmissionApplicant source, List<AdmissionInquiry> sourceInquiries) =>
		sourceInquiries.Where(row => row.ApplicantId == source.Id && row.AdmissionCycleId == inquiry.AdmissionCycleId)
			.Select(row => row.ApplicantName).DefaultIfEmpty(string.Join(" ", new[] { source.FirstName, source.MiddleName, source.LastName }.Where(value => !string.IsNullOrWhiteSpace(value))))
			.Any(name => AdmissionInquiryStudentDuplicateGuard.IsSameIdentity(inquiry.ApplicantName, target.DateOfBirth, name, source.DateOfBirth));
	private static string NormalizePhone(string? value)
	{
		var digits = string.Concat((value ?? string.Empty).Where(char.IsDigit));
		return digits.Length == 12 && digits.StartsWith("91", StringComparison.Ordinal) ? digits[2..] : digits;
	}
	private static string NormalizeEmail(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;
	private sealed record Contact(HashSet<string> Phones, HashSet<string> Emails);
	private sealed record Context(Dictionary<Guid, Contact> Contacts, List<AdmissionDirectFamilyMatchDto> Families,
		List<AdmissionApplicant> VisibleApplicants, List<AdmissionApplicant> TargetApplicants, List<AdmissionInquiry> SourceInquiries);
}
