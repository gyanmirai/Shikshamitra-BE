using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmAdmissionDecisionQueryWorkflow(
	IRepository<AdmissionCrmAdmissionDecision, Guid> repository,
	AdmissionCrmFinalAdmissionAccess access) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmAdmissionDecisionDto?> GetAsync(Guid leadId)
	{
		await access.GetLeadAsync(leadId, "GetDecisionAsync");
		var queryable = await repository.GetQueryableAsync();
		var decision = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id &&
			x.LeadId == leadId).OrderByDescending(x => x.DecidedAt));
		return decision == null ? null : AdmissionCrmFinalAdmissionMapper.Decision(decision);
	}

	public async Task<IReadOnlyList<AdmissionCrmAdmissionDecisionDto>> GetHistoryAsync(Guid leadId)
	{
		await access.GetLeadAsync(leadId, "GetDecisionHistoryAsync");
		var queryable = await repository.GetQueryableAsync();
		var decisions = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id &&
			x.LeadId == leadId).OrderByDescending(x => x.DecidedAt));
		return decisions.Select(AdmissionCrmFinalAdmissionMapper.Decision).ToList();
	}
}
