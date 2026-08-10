using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPlaybookResolutionWorkflow(
	IRepository<AdmissionCrmPlaybook, Guid> playbookRepository,
	AdmissionCrmPlaybookAccess access)
	: ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmPlaybookResolutionDto> ResolveAsync(Guid academicYearId, ResolveAdmissionCrmPlaybookDto input)
	{
		await access.EnsureYearAsync(academicYearId, nameof(ResolveAsync));
		AdmissionCrmPlaybookApplicability.Normalize(input);
		var queryable = await playbookRepository.GetQueryableAsync();
		var published = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId && x.Status == AdmissionCrmPlaybookStatus.Published));
		var matches = published.Select(x => new
			{
				Playbook = x,
				Applicability = AdmissionCrmPlaybookMapper.Deserialize<AdmissionCrmPlaybookApplicabilityDto>(x.ApplicabilityJson)
			})
			.Where(x => AdmissionCrmPlaybookApplicability.Matches(x.Applicability, input))
			.Select(x => new { x.Playbook, Score = x.Playbook.IsFallback ? 0 : AdmissionCrmPlaybookApplicability.Specificity(x.Applicability) })
			.OrderByDescending(x => x.Score).ThenByDescending(x => x.Playbook.PublishedAt).ToList();
		if (matches.Count == 0) throw new BusinessException("AdmissionCrm:NoApplicablePlaybook").WithData("AcademicYearId", academicYearId);
		if (matches.Count > 1 && matches[0].Score == matches[1].Score)
			throw new BusinessException("AdmissionCrm:AmbiguousPlaybookResolution")
				.WithData("AcademicYearId", academicYearId).WithData("SpecificityScore", matches[0].Score);
		return new AdmissionCrmPlaybookResolutionDto
		{
			Playbook = AdmissionCrmPlaybookMapper.ToDto(matches[0].Playbook), SpecificityScore = matches[0].Score,
			Explanation = matches[0].Playbook.IsFallback ? "Resolved to the published fallback playbook." :
				$"Resolved to the most specific published playbook using {matches[0].Score} matching dimensions."
		};
	}
}
