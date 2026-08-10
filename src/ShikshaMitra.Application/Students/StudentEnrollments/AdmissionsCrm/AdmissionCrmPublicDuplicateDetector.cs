using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPublicDuplicateDetector(
	IRepository<AdmissionInquiryLead, Guid> leadRepository) : ApplicationService, ITransientDependency
{
	public async Task<int> CountLikelyDuplicatesAsync(AdmissionInquiryLead lead)
	{
		var profile = AdmissionCrmDuplicateProfileProbe.FromLead(lead);
		var hasContact = !string.IsNullOrWhiteSpace(lead.NormalizedPrimaryContactNo) ||
			!string.IsNullOrWhiteSpace(lead.NormalizedEmail);
		if (!hasContact && !profile.HasMinimumProfile())
		{
			return 0;
		}

		var queryable = await leadRepository.GetQueryableAsync();
		var candidates = await AsyncExecuter.ToListAsync(queryable.Where(candidate =>
			candidate.TenantId == CurrentTenant.Id &&
			candidate.AcademicYearId == lead.AcademicYearId &&
			candidate.Id != lead.Id &&
			candidate.MergedIntoLeadId == null &&
			((lead.NormalizedPrimaryContactNo != null &&
			  candidate.NormalizedPrimaryContactNo == lead.NormalizedPrimaryContactNo) ||
			 (lead.NormalizedEmail != null && candidate.NormalizedEmail == lead.NormalizedEmail) ||
			 (profile.HasMinimumProfile() && candidate.FirstName != null &&
			  candidate.FirstName.ToLower() == profile.FirstName &&
			  (candidate.ParentGuardianName != null || candidate.DateOfBirth.HasValue) &&
			  ((profile.TargetStandardId.HasValue && candidate.TargetStandardId == profile.TargetStandardId.Value) ||
			   (profile.DesiredClassName != null && candidate.DesiredClassName != null &&
				candidate.DesiredClassName.ToLower() == profile.DesiredClassName))))));
		return candidates.Count(candidate => AdmissionCrmLeadDuplicateMatcher.AreLikelyDuplicates(lead, candidate));
	}
}
