using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmWaitlistQueryWorkflow(
	IRepository<AdmissionCrmWaitlistEntry, Guid> waitlistRepository,
	IRepository<AdmissionCrmWaitlistHistory, Guid> historyRepository,
	IRepository<AdmissionInquiryLead, Guid> leadRepository,
	StudentScopeAccessService scopeAccess,
	AdmissionCrmFinalAdmissionAccess access,
	AdmissionCrmFinalAdmissionMapper mapper) : ApplicationService, ITransientDependency
{
	public async Task<IReadOnlyList<AdmissionCrmWaitlistEntryDto>> GetAsync(Guid poolId)
	{
		await access.GetPoolAsync(poolId, "GetWaitlistAsync");
		var leads = await leadRepository.GetQueryableAsync();
		var scoped = await scopeAccess.ApplyCurrentUserAdmissionLeadScopeAsync(leads.Where(x => x.TenantId == CurrentTenant.Id));
		var leadIds = scoped.Select(x => x.Id);
		var queryable = await waitlistRepository.GetQueryableAsync();
		var entries = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id &&
			x.SeatPoolId == poolId && leadIds.Contains(x.LeadId)).OrderBy(x => x.Status == AdmissionCrmWaitlistStatus.Active ? 0 : 1)
			.ThenByDescending(x => x.PriorityScore).ThenBy(x => x.PositionSequence));
		var position = 0;
		var results = new List<AdmissionCrmWaitlistEntryDto>();
		foreach (var entry in entries)
		{
			if (entry.Status == AdmissionCrmWaitlistStatus.Active) position++;
			results.Add(await mapper.WaitlistAsync(entry, entry.Status == AdmissionCrmWaitlistStatus.Active ? position : 0));
		}
		return results;
	}

	public async Task<IReadOnlyList<AdmissionCrmWaitlistHistoryDto>> GetHistoryAsync(Guid id)
	{
		var entry = await access.GetWaitlistEntryAsync(id, "GetWaitlistHistoryAsync");
		var queryable = await historyRepository.GetQueryableAsync();
		var items = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id && x.WaitlistEntryId == entry.Id));
		return items.OrderBy(x => x.OccurredAt).ThenBy(x => x.Id).Select(AdmissionCrmFinalAdmissionMapper.WaitlistHistory).ToList();
	}
}
