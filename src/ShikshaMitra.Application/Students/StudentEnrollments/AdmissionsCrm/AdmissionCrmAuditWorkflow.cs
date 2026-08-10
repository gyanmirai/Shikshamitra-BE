using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmAuditWorkflow(
	IRepository<AdmissionCrmTimelineEvent, Guid> timelineRepository,
	AdmissionCrmGovernanceAccess access)
	: ApplicationService, ITransientDependency
{
	public async Task<PagedResultDto<AdmissionCrmTimelineEventDto>> SearchAsync(GetAdmissionCrmAuditInput input)
	{
		await access.EnsureAcademicYearAccessAsync(input.AcademicYearId, "SearchAuditAsync");
		var scopedLeadIds = await access.GetScopedLeadIdsAsync(input.AcademicYearId);
		var queryable = await timelineRepository.GetQueryableAsync();
		var query = queryable.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == input.AcademicYearId &&
			scopedLeadIds.Contains(x.LeadId));
		if (input.LeadId.HasValue) query = query.Where(x => x.LeadId == input.LeadId);
		if (input.ActorUserId.HasValue) query = query.Where(x => x.ActorUserId == input.ActorUserId);
		if (input.EventType.HasValue) query = query.Where(x => x.EventType == input.EventType);
		if (input.From.HasValue) query = query.Where(x => x.OccurredAt >= input.From.Value);
		if (input.To.HasValue) query = query.Where(x => x.OccurredAt <= input.To.Value);
		var persistedItems = await AsyncExecuter.ToListAsync(query);
		var items = persistedItems.OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id)
			.Skip(input.SkipCount).Take(Math.Min(input.MaxResultCount, 500));
		return new PagedResultDto<AdmissionCrmTimelineEventDto>(persistedItems.Count, items.Select(AdmissionCrmGovernanceMapper.Timeline).ToList());
	}
}
