using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionDirectFamilyMatchService(
	IRepository<AdmissionFamily, Guid> families,
	IRepository<AdmissionGuardian, Guid> guardians,
	IRepository<AdmissionApplicant, Guid> applicants,
	IAdmissionApplicantRepository parentApplicants,
	IStudentRepository students,
	AdmissionInquiryScopeAccessService inquiryScope,
	AdmissionApplicationScopeAccessService applicationScope,
	AdmissionOfferingRuntimeService offeringRuntime)
	: ApplicationService, ITransientDependency
{
	public Task<List<AdmissionDirectFamilyMatchDto>> FindAsync(Guid offeringId, string guardianMobile, string? guardianEmail = null) =>
		FindCoreAsync(offeringId, guardianMobile, guardianEmail, requireSpecialTrack: false);

	internal Task<List<AdmissionDirectFamilyMatchDto>> FindForSpecialRegistrationAsync(Guid offeringId, string guardianMobile, string? guardianEmail = null) =>
		FindCoreAsync(offeringId, guardianMobile, guardianEmail, requireSpecialTrack: true);

	private async Task<List<AdmissionDirectFamilyMatchDto>> FindCoreAsync(Guid offeringId, string guardianMobile, string? guardianEmail, bool requireSpecialTrack)
	{
		var offering = (await offeringRuntime.GetAvailableAsync(offeringId)).Offering;
		if (requireSpecialTrack)
			AdmissionDirectRegistrationEntryPolicy.EnsureAllowed(offering);
		if (offering.EntryMode is not (AdmissionEntryMode.DirectRegistration or AdmissionEntryMode.Both))
			throw new BusinessException(AdmissionErrorCodes.DirectRegistrationEntryNotAllowed);
		var phone = string.IsNullOrWhiteSpace(guardianMobile) ? null : AdmissionInquiryPhoneValidator.NormalizeAndValidateIndianMobile(guardianMobile);
		return await FindAccessibleMatchesAsync(phone, guardianEmail, includeLegacyGuardians: guardianEmail == null);
	}

	public async Task EnsureCanLinkAsync(Guid familyId, string normalizedMobile,
		string errorCode = AdmissionErrorCodes.DirectRegistrationFamilyMismatch)
	{
		var matches = await FindAccessibleMatchesAsync(normalizedMobile, null, includeLegacyGuardians: true);
		if (matches.All(match => match.FamilyId != familyId))
			throw new BusinessException(errorCode,
				"This family is not available in your admission scope for the supplied guardian mobile.");
	}

	public async Task<AdmissionSiblingParentCandidateDto> ResolveParentAsync(Guid familyId, Guid sourceApplicantId,
		string? relationship, string mobile, string? email, string errorCode)
	{
		if (!AdmissionSiblingParentSelection.IsRelationship(relationship)) throw new BusinessException(errorCode, "Choose Father or Mother.");
		var matches = await FindAccessibleMatchesAsync(mobile, email);
		var parents = matches.FirstOrDefault(match => match.FamilyId == familyId)?.Parents;
		var parent = parents?.FirstOrDefault(candidate => candidate.SourceApplicantId == sourceApplicantId && candidate.Relationship == relationship)
			?? parents?.FirstOrDefault(candidate => candidate.SourceApplicantId == sourceApplicantId && candidate.Relationship == null);
		if (parent == null || !Matches(parent, NormalizePhones([mobile]), NormalizeEmails([email])))
			throw new BusinessException(errorCode, "The selected parent no longer matches this family and contact. Check the family again.");
		parent.Relationship = relationship;
		return parent;
	}

	internal async Task<List<AdmissionDirectFamilyMatchDto>> FindAccessibleMatchesAsync(string? mobile, string? email,
		IReadOnlyCollection<AdmissionSiblingParentContactDto>? contacts = null, bool includeLegacyGuardians = false)
	{
		if (contacts?.Count > 2 || contacts?.Any(contact => !AdmissionSiblingParentSelection.IsRelationship(contact.Relationship)) == true)
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput, "Choose Father or Mother for each parent contact.");
		var phones = NormalizePhones((contacts ?? []).Select(contact => contact.Mobile).Append(mobile));
		var emails = NormalizeEmails((contacts ?? []).Select(contact => contact.Email).Append(email));
		if (phones.Length == 0 && emails.Length == 0) return [];
		var scopedApplications = await applicationScope.GetScopedApplicationsAsync();
		var scopedInquiries = await inquiryScope.GetScopedQueryAsync();
		var visibleApplicants = (await applicants.GetQueryableAsync()).Where(applicant =>
			applicant.TenantId == CurrentTenant.Id &&
			(scopedApplications.Any(application => application.ApplicantId == applicant.Id) ||
			 scopedInquiries.Any(inquiry => inquiry.ApplicantId == applicant.Id)));
		var matchingGuardians = await guardians.GetListAsync(parent =>
			(parent.Relationship == AdmissionGuardianRelationship.Parent || includeLegacyGuardians && parent.IsPrimaryContact) &&
			(Enumerable.Contains(phones, parent.Mobile) || parent.Email != null && Enumerable.Contains(emails, parent.Email.Trim().ToLower())));
		var matchedFamilyIds = matchingGuardians.Select(parent => parent.FamilyId).ToHashSet();
		var studentQuery = await students.GetQueryableWithoutAutoIncludesAsync();
		var matchingPhones = phones.Concat(phones.Select(phone => "91" + phone)).ToArray();
		var matchingStudentIds = await AsyncExecuter.ToListAsync(studentQuery.Where(student => student.FamilyDetail != null &&
			(student.FamilyDetail.FatherDetails != null &&
				(student.FamilyDetail.FatherDetails.Email != null && Enumerable.Contains(emails, student.FamilyDetail.FatherDetails.Email.Trim().ToLower()) ||
				 student.FamilyDetail.FatherDetails.ContactNo != null && Enumerable.Contains(matchingPhones, student.FamilyDetail.FatherDetails.ContactNo.Replace(" ", "").Replace("+", "").Replace("-", "").Replace("(", "").Replace(")", ""))) ||
			 student.FamilyDetail.MotherDetails != null &&
				(student.FamilyDetail.MotherDetails.Email != null && Enumerable.Contains(emails, student.FamilyDetail.MotherDetails.Email.Trim().ToLower()) ||
				 student.FamilyDetail.MotherDetails.ContactNo != null && Enumerable.Contains(matchingPhones, student.FamilyDetail.MotherDetails.ContactNo.Replace(" ", "").Replace("+", "").Replace("-", "").Replace("(", "").Replace(")", "")))))
			.Select(student => student.Id));
		var linkedFamilyIds = await AsyncExecuter.ToListAsync(visibleApplicants.Where(applicant =>
			applicant.ExistingStudentId.HasValue && matchingStudentIds.Contains(applicant.ExistingStudentId.Value) ||
			matchingStudentIds.Contains(applicant.ReservedStudentId))
			.Select(applicant => applicant.FamilyId).Distinct());
		matchedFamilyIds.UnionWith(linkedFamilyIds);
		var pendingMatches = await parentApplicants.GetParentContactMatchesAsync(emails, phones);
		var pendingFamilyIds = await AsyncExecuter.ToListAsync(pendingMatches.Where(applicant =>
			visibleApplicants.Any(visible => visible.Id == applicant.Id)).Select(applicant => applicant.FamilyId).Distinct());
		matchedFamilyIds.UnionWith(pendingFamilyIds);
		if (matchedFamilyIds.Count == 0) return [];
		var ids = matchedFamilyIds.ToArray();
		var familyApplicants = await AsyncExecuter.ToListAsync(visibleApplicants.Where(applicant => Enumerable.Contains(ids, applicant.FamilyId)));
		var familyIds = familyApplicants.Select(applicant => applicant.FamilyId).Distinct().ToArray();
		if (familyIds.Length == 0) return [];
		var familyRows = await families.GetListAsync(family => Enumerable.Contains(familyIds, family.Id) && family.Status == AdmissionFamilyStatus.Active);
		var familyGuardians = await guardians.GetListAsync(parent => Enumerable.Contains(familyIds, parent.FamilyId) && parent.IsPrimaryContact);
		var existingIds = familyApplicants.Select(applicant => applicant.ExistingStudentId ?? applicant.ReservedStudentId).Distinct().ToArray();
		var profiles = await AsyncExecuter.ToListAsync(studentQuery.Where(student => Enumerable.Contains(existingIds, student.Id))
			.Select(student => new { student.Id, student.FamilyDetail }));
		return familyRows.Select(family =>
		{
			var primary = familyGuardians.FirstOrDefault(parent => parent.Id == family.PrimaryGuardianId);
			var parents = new List<AdmissionSiblingParentCandidateDto>();
			foreach (var applicant in familyApplicants.Where(applicant => applicant.FamilyId == family.Id))
			{
				var profile = profiles.FirstOrDefault(student => student.Id == (applicant.ExistingStudentId ?? applicant.ReservedStudentId))?.FamilyDetail;
				foreach (var prefix in new[] { "Father", "Mother" })
				{
					var saved = prefix == "Father" ? profile?.FatherDetails : profile?.MotherDetails;
					var candidate = ReadParent(applicant.Id, prefix, key => AdmissionStudentProfileMapping.Read(applicant, key));
					if (saved != null) candidate = CreateCandidate(applicant.Id, prefix.ToLowerInvariant(), saved.FullName, saved.ContactNo, saved.Email);
					if (!string.IsNullOrWhiteSpace(candidate.Email) || !string.IsNullOrWhiteSpace(candidate.Mobile)) parents.Add(candidate);
				}
				if (primary != null && profile?.FatherDetails == null && profile?.MotherDetails == null &&
					primary.Relationship == AdmissionGuardianRelationship.Parent && !parents.Any(parent => parent.SourceApplicantId == applicant.Id &&
					(string.Equals(parent.Email, primary.Email, StringComparison.OrdinalIgnoreCase) || parent.Mobile == primary.Mobile)))
					parents.Add(CreateCandidate(applicant.Id, null, string.Join(" ", new[] { primary.FirstName, primary.LastName }.Where(value => !string.IsNullOrWhiteSpace(value))), primary.Mobile, primary.Email));
			}
			return new AdmissionDirectFamilyMatchDto
			{
				FamilyId = family.Id, FamilyNumber = family.FamilyNumber,
				PrimaryGuardianName = primary == null ? parents.FirstOrDefault()?.Name ?? string.Empty : string.Join(" ", new[] { primary.FirstName, primary.LastName }.Where(value => !string.IsNullOrWhiteSpace(value))),
				Applicants = familyApplicants.Where(applicant => applicant.FamilyId == family.Id).Select(applicant => string.Join(" ", new[] { applicant.FirstName, applicant.LastName }.Where(value => !string.IsNullOrWhiteSpace(value)))).ToList(),
				Parents = parents, MatchByEmail = parents.Any(parent => parent.Email != null && emails.Contains(parent.Email.ToLowerInvariant())),
				MatchByMobile = parents.Any(parent => parent.Mobile != null && phones.Contains(parent.Mobile)) ||
					includeLegacyGuardians && matchingGuardians.Any(parent => parent.FamilyId == family.Id && phones.Contains(parent.Mobile))
			};
		}).Where(match => match.MatchByEmail || match.MatchByMobile).ToList();
	}

	private static AdmissionSiblingParentCandidateDto ReadParent(Guid applicantId, string prefix, Func<string, string?> read) =>
		CreateCandidate(applicantId, prefix.ToLowerInvariant(), read(prefix + ".Name"), read(prefix + ".Mobile"), read(prefix + ".Email"));
	private static AdmissionSiblingParentCandidateDto CreateCandidate(Guid applicantId, string? relationship, string? name, string? mobile, string? email)
	{
		var parts = (name ?? string.Empty).Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
		return new() { SourceApplicantId = applicantId, Relationship = relationship, Name = name?.Trim() ?? string.Empty,
			FirstName = parts.FirstOrDefault() ?? string.Empty, LastName = parts.Length > 1 ? parts[1] : null,
			Email = AdmissionSiblingParentSelection.NormalizeEmail(email), Mobile = NormalizeMobile(mobile) };
	}
	private static bool Matches(AdmissionSiblingParentCandidateDto parent, string[] phones, string[] emails) =>
		parent.Mobile != null && phones.Contains(parent.Mobile) || parent.Email != null && emails.Contains(parent.Email.ToLowerInvariant());
	private static string? NormalizeMobile(string? mobile)
	{
		if (string.IsNullOrWhiteSpace(mobile)) return null;
		var digits = string.Concat(mobile.Where(char.IsDigit));
		return digits.Length == 12 && digits.StartsWith("91", StringComparison.Ordinal) ? digits[2..] : digits;
	}
	private static string[] NormalizePhones(IEnumerable<string?> phones) => phones.Select(NormalizeMobile).Where(phone => phone?.Length == 10).Cast<string>().Distinct().ToArray();
	private static string[] NormalizeEmails(IEnumerable<string?> emails) => emails.Select(AdmissionSiblingParentSelection.NormalizeEmail).Where(email => email != null).Cast<string>().Distinct().ToArray();
}
