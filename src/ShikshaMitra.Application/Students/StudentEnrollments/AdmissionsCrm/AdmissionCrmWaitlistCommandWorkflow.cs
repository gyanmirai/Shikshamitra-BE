using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmWaitlistCommandWorkflow(
	IRepository<AdmissionCrmWaitlistEntry, Guid> waitlistRepository,
	IRepository<AdmissionCrmSeatPool, Guid> poolRepository,
	IRepository<AdmissionInquiryLead, Guid> leadRepository,
	AdmissionCrmFinalAdmissionAccess access,
	AdmissionCrmFinalAdmissionMapper mapper,
	AdmissionCrmFinalAdmissionTimeline timeline,
	AdmissionCrmWaitlistHistoryWorkflow history) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmWaitlistEntryDto> AddAsync(Guid leadId, CreateAdmissionCrmWaitlistEntryDto input)
	{
		var lead = await access.GetLeadAsync(leadId, "AddToWaitlistAsync");
		var pool = await access.GetPoolAsync(input.SeatPoolId, "AddToWaitlistAsync");
		if (pool.AcademicYearId != lead.AcademicYearId) throw new BusinessException("AdmissionCrm:SeatPoolAcademicYearMismatch");
		var queryable = await waitlistRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id && x.LeadId == leadId &&
			x.SeatPoolId == pool.Id && x.Status == AdmissionCrmWaitlistStatus.Active)))
			throw new BusinessException("AdmissionCrm:LeadAlreadyWaitlisted");
		var last = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id &&
			x.SeatPoolId == pool.Id).OrderByDescending(x => x.PositionSequence));
		var entry = new AdmissionCrmWaitlistEntry(GuidGenerator.Create(), CurrentTenant.Id, lead.AcademicYearId,
			lead.Id, pool.Id, input.PriorityScore, (last?.PositionSequence ?? 0) + 1, Clock.Now);
		pool.AddWaitlistedApplicant();
		lead.ChangeStage(AdmissionInquiryStage.Waitlisted);
		await waitlistRepository.InsertAsync(entry, autoSave: true);
		await poolRepository.UpdateAsync(pool, autoSave: true);
		await leadRepository.UpdateAsync(lead, autoSave: true);
		await timeline.AddAsync(lead, AdmissionCrmTimelineEventType.Waitlisted, "Applicant waitlisted",
			$"Applicant was added to seat pool {pool.Code} with priority score {entry.PriorityScore}.", entry.Id);
		await history.AddAsync(entry, AdmissionCrmWaitlistHistoryAction.Added,
			$"Applicant added with priority score {entry.PriorityScore}.");
		return await mapper.WaitlistAsync(entry, 0);
	}

	public async Task<AdmissionCrmWaitlistEntryDto> WithdrawAsync(Guid id, AdmissionCrmReasonDto input)
	{
		var entry = await access.GetWaitlistEntryAsync(id, "WithdrawFromWaitlistAsync");
		var pool = await access.GetPoolAsync(entry.SeatPoolId, "WithdrawFromWaitlistAsync");
		entry.Withdraw(input.Reason, Clock.Now);
		await history.AddAsync(entry, AdmissionCrmWaitlistHistoryAction.Withdrawn, input.Reason);
		pool.RemoveWaitlistedApplicant();
		await waitlistRepository.UpdateAsync(entry, autoSave: true);
		await poolRepository.UpdateAsync(pool, autoSave: true);
		var lead = await access.GetLeadAsync(entry.LeadId, "WithdrawFromWaitlistAsync");
		await timeline.AddAsync(lead, AdmissionCrmTimelineEventType.SeatReleased, "Waitlist entry withdrawn", input.Reason, entry.Id);
		return await mapper.WaitlistAsync(entry, 0);
	}

	public async Task<AdmissionCrmWaitlistEntryDto> UpdateGovernanceAsync(Guid id, UpdateAdmissionCrmWaitlistGovernanceDto input)
	{
		var entry = await access.GetWaitlistEntryAsync(id, "UpdateWaitlistGovernanceAsync");
		var previousPriority = entry.PriorityScore;
		var wasProtected = entry.IsProtected;
		var wasPaused = entry.PromotionPaused;
		entry.UpdateGovernance(input.PriorityScore, input.IsProtected, input.PromotionPaused, input.Reason);
		await waitlistRepository.UpdateAsync(entry, autoSave: true);
		if (previousPriority != entry.PriorityScore) await history.AddAsync(entry, AdmissionCrmWaitlistHistoryAction.PriorityChanged,
			input.Reason, previousPriorityScore: previousPriority);
		if (wasProtected != entry.IsProtected) await history.AddAsync(entry, entry.IsProtected
			? AdmissionCrmWaitlistHistoryAction.Protected : AdmissionCrmWaitlistHistoryAction.ProtectionRemoved, input.Reason);
		if (wasPaused != entry.PromotionPaused) await history.AddAsync(entry, entry.PromotionPaused
			? AdmissionCrmWaitlistHistoryAction.PromotionPaused : AdmissionCrmWaitlistHistoryAction.PromotionResumed, input.Reason);
		var lead = await access.GetLeadAsync(entry.LeadId, "UpdateWaitlistGovernanceAsync");
		await timeline.AddAsync(lead, AdmissionCrmTimelineEventType.WaitlistGovernanceChanged,
			"Waitlist governance updated", input.Reason, entry.Id);
		return await mapper.WaitlistAsync(entry, 0);
	}
}
