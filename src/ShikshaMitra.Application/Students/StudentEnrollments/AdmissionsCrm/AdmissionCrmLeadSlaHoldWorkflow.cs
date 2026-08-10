using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmLeadSlaHoldWorkflow(
	IRepository<AdmissionCrmSlaHold, Guid> slaHoldRepository,
	AdmissionCrmLeadAccess access,
	AdmissionCrmLeadTimelineWriter timeline) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmSlaHoldDto> CreateSlaHoldAsync(Guid leadId, CreateAdmissionCrmSlaHoldDto input)
	{
		var lead = await access.GetLeadForCurrentTenantAsync(leadId, nameof(CreateSlaHoldAsync));
		var queryable = await slaHoldRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id && x.LeadId == leadId && x.ResumedAt == null)))
			throw new BusinessException("AdmissionCrm:ActiveSlaHoldAlreadyExists");
		var hold = new AdmissionCrmSlaHold(GuidGenerator.Create(), CurrentTenant.Id, lead.AcademicYearId,
			lead.Id, input.HoldReason, input.Reason, GetCurrentUserId(), Clock.Now);
		await slaHoldRepository.InsertAsync(hold, autoSave: true);
		await timeline.AddTimelineEventAsync(lead, AdmissionCrmTimelineEventType.SlaHoldCreated, "SLA clock paused", input.Reason, hold.Id);
		return AdmissionCrmLeadMapper.MapSlaHold(hold);
	}

	public async Task<AdmissionCrmSlaHoldDto> ResumeSlaHoldAsync(Guid holdId, ResumeAdmissionCrmSlaHoldDto input)
	{
		var hold = await slaHoldRepository.GetAsync(holdId);
		var lead = await access.GetLeadForCurrentTenantAsync(hold.LeadId, nameof(ResumeSlaHoldAsync));
		hold.Resume(GetCurrentUserId(), input.Reason, Clock.Now);
		await slaHoldRepository.UpdateAsync(hold, autoSave: true);
		await timeline.AddTimelineEventAsync(lead, AdmissionCrmTimelineEventType.SlaHoldResumed, "SLA clock resumed", input.Reason, hold.Id);
		return AdmissionCrmLeadMapper.MapSlaHold(hold);
	}

	public async Task<IReadOnlyList<AdmissionCrmSlaHoldDto>> GetSlaHoldsAsync(Guid leadId)
	{
		var lead = await access.GetLeadForCurrentTenantAsync(leadId, nameof(GetSlaHoldsAsync));
		var queryable = await slaHoldRepository.GetQueryableAsync();
		var items = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id && x.LeadId == lead.Id));
		return items.OrderByDescending(x => x.StartedAt).Select(AdmissionCrmLeadMapper.MapSlaHold).ToList();
	}

	private Guid GetCurrentUserId() => CurrentUser.Id ??
		throw new Volo.Abp.Authorization.AbpAuthorizationException("Authenticated user required.");
}