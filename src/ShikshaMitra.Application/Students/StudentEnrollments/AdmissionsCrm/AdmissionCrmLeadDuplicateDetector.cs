using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmLeadDuplicateDetector(
	IRepository<AdmissionInquiryLead, Guid> leadRepository,
	StudentScopeAccessService scopeAccess) : ApplicationService, ITransientDependency
{
	internal async Task<IReadOnlyList<AdmissionCrmLeadDuplicateDto>> FindDuplicatesAsync(
		Guid academicYearId,
		string? normalizedPhone,
		string? normalizedEmail,
		AdmissionCrmDuplicateProfileProbe profile,
		Guid? excludeLeadId)
	{
		var hasContactCriteria =
			!string.IsNullOrWhiteSpace(normalizedPhone) ||
			!string.IsNullOrWhiteSpace(normalizedEmail);
		var hasProfileCriteria = profile.HasMinimumProfile();
		if (!hasContactCriteria && !hasProfileCriteria)
		{
			return Array.Empty<AdmissionCrmLeadDuplicateDto>();
		}

		var queryable = await leadRepository.GetQueryableAsync();
		var query = queryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId &&
			x.MergedIntoLeadId == null &&
			(!excludeLeadId.HasValue || x.Id != excludeLeadId.Value) &&
			((normalizedPhone != null && x.NormalizedPrimaryContactNo == normalizedPhone) ||
			 (normalizedEmail != null && x.NormalizedEmail == normalizedEmail) ||
			 (hasProfileCriteria &&
			  x.FirstName != null &&
			  x.FirstName.ToLower() == profile.FirstName &&
			  (x.ParentGuardianName != null || x.DateOfBirth.HasValue) &&
			  ((profile.TargetStandardId.HasValue && x.TargetStandardId == profile.TargetStandardId.Value) ||
			   (profile.DesiredClassName != null &&
				x.DesiredClassName != null &&
				x.DesiredClassName.ToLower() == profile.DesiredClassName)))));
		query = await scopeAccess.ApplyCurrentUserAdmissionLeadScopeAsync(query);

		var matches = await AsyncExecuter.ToListAsync(
			query
				.OrderByDescending(x => x.CreationTime)
				.Take(10));

		return matches
			.Where(x => AdmissionCrmLeadDuplicateMatcher.IsLikelyDuplicate(x, normalizedPhone, normalizedEmail, profile))
			.Select(x => AdmissionCrmLeadDuplicateMatcher.ToDuplicateDto(x, normalizedPhone, normalizedEmail, profile))
			.ToList();
	}

	internal async Task<IReadOnlyDictionary<Guid, IReadOnlyList<AdmissionCrmLeadDuplicateDto>>> FindDuplicatesForLeadsAsync(
		Guid academicYearId,
		IReadOnlyList<AdmissionInquiryLead> leads)
	{
		var phones = leads
			.Select(x => x.NormalizedPrimaryContactNo)
			.Where(x => !string.IsNullOrWhiteSpace(x))
			.Select(x => x!)
			.Distinct()
			.ToList();
		var emails = leads
			.Select(x => x.NormalizedEmail)
			.Where(x => !string.IsNullOrWhiteSpace(x))
			.Select(x => x!)
			.Distinct()
			.ToList();
		var profileProbes = leads
			.Select(AdmissionCrmDuplicateProfileProbe.FromLead)
			.Where(x => x.HasMinimumProfile())
			.ToList();
		if (phones.Count == 0 && emails.Count == 0 && profileProbes.Count == 0)
		{
			return new Dictionary<Guid, IReadOnlyList<AdmissionCrmLeadDuplicateDto>>();
		}

		var profileFirstNames = profileProbes
			.Select(x => x.FirstName!)
			.Distinct()
			.ToList();
		var profileTargetStandardIds = profileProbes
			.Select(x => x.TargetStandardId)
			.Where(x => x.HasValue)
			.Select(x => x!.Value)
			.Distinct()
			.ToList();
		var profileClassNames = profileProbes
			.Select(x => x.DesiredClassName)
			.Where(x => !string.IsNullOrWhiteSpace(x))
			.Select(x => x!)
			.Distinct()
			.ToList();
		var hasProfileCriteria = profileProbes.Count > 0;
		var queryable = await leadRepository.GetQueryableAsync();
		var candidateQuery = queryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId &&
			x.MergedIntoLeadId == null &&
			((x.NormalizedPrimaryContactNo != null && phones.Contains(x.NormalizedPrimaryContactNo)) ||
			 (x.NormalizedEmail != null && emails.Contains(x.NormalizedEmail)) ||
			 (hasProfileCriteria &&
			  x.FirstName != null &&
			  profileFirstNames.Contains(x.FirstName.ToLower()) &&
			  (x.ParentGuardianName != null || x.DateOfBirth.HasValue) &&
			  ((x.TargetStandardId.HasValue && profileTargetStandardIds.Contains(x.TargetStandardId.Value)) ||
			   (x.DesiredClassName != null && profileClassNames.Contains(x.DesiredClassName.ToLower()))))));
		candidateQuery = await scopeAccess.ApplyCurrentUserAdmissionLeadScopeAsync(candidateQuery);
		var candidates = await AsyncExecuter.ToListAsync(candidateQuery);

		return leads.ToDictionary(
			lead => lead.Id,
			lead => (IReadOnlyList<AdmissionCrmLeadDuplicateDto>) candidates
				.Where(candidate => candidate.Id != lead.Id && AdmissionCrmLeadDuplicateMatcher.AreLikelyDuplicates(lead, candidate))
				.OrderByDescending(candidate => candidate.CreationTime)
				.Take(10)
				.Select(candidate => AdmissionCrmLeadDuplicateMatcher.ToDuplicateDto(
					candidate,
					lead.NormalizedPrimaryContactNo,
					lead.NormalizedEmail,
					AdmissionCrmDuplicateProfileProbe.FromLead(lead)))
				.ToList());
	}
}