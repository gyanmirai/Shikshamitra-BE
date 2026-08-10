using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmWaitlistPromotionWorkflow(
	IRepository<AdmissionCrmWaitlistEntry, Guid> waitlistRepository,
	IRepository<AdmissionCrmSeatOffer, Guid> offerRepository,
	IRepository<AdmissionInquiryLead, Guid> leadRepository,
	StudentScopeAccessService scopeAccess,
	AdmissionCrmFinalAdmissionAccess access,
	AdmissionCrmOfferDraftWorkflow offers,
	AdmissionCrmFinalAdmissionTimeline timeline,
	AdmissionCrmWaitlistHistoryWorkflow history) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmSeatOfferDto> PrepareAsync(Guid id, PrepareAdmissionCrmWaitlistPromotionDto input)
	{
		var entry = await access.GetWaitlistEntryAsync(id, "PrepareWaitlistPromotionAsync");
		if (entry.Status != AdmissionCrmWaitlistStatus.Active) throw new BusinessException("AdmissionCrm:WaitlistEntryNotActive");
		if (input.SeatPoolId != entry.SeatPoolId) throw new BusinessException("AdmissionCrm:WaitlistSeatPoolMismatch");
		var queryable = await waitlistRepository.GetQueryableAsync();
		var active = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id &&
			x.SeatPoolId == entry.SeatPoolId && x.Status == AdmissionCrmWaitlistStatus.Active && !x.PromotionPaused));
		var next = active.OrderByDescending(x => x.PriorityScore).ThenBy(x => x.PositionSequence).FirstOrDefault();
		if (next?.Id != entry.Id)
		{
			if (!input.OverridePriority) throw new BusinessException("AdmissionCrm:HigherPriorityWaitlistEntryExists");
			if (!await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.OverrideEligibility))
				throw new AbpAuthorizationException("Waitlist-priority override permission is required.");
			if (string.IsNullOrWhiteSpace(input.OverrideReason)) throw new BusinessException("AdmissionCrm:WaitlistPriorityOverrideReasonRequired");
			var lead = await access.GetLeadAsync(entry.LeadId, "PrepareWaitlistPromotionAsync");
			await timeline.AddAsync(lead, AdmissionCrmTimelineEventType.WaitlistPromotionOverridden,
				"Waitlist priority overridden", input.OverrideReason, entry.Id);
		}
		var offer = await offers.CreateAsync(entry.LeadId, input);
		await history.AddAsync(entry, AdmissionCrmWaitlistHistoryAction.OfferPrepared,
			"Waitlist promotion prepared a draft offer for approval.", offer.Id);
		return offer;
	}

	public async Task<AdmissionCrmWaitlistPromotionRunResultDto> RunAsync(Guid yearId, RunAdmissionCrmWaitlistPromotionDto input)
	{
		await scopeAccess.EnsureCanAccessAcademicYearAsync(yearId, "RunWaitlistPromotionAsync");
		if (input.MaximumOffers is < 1 or > 100) throw new BusinessException("AdmissionCrm:InvalidWaitlistPromotionBatch");
		var pool = await access.GetPoolAsync(input.SeatPoolId, "RunWaitlistPromotionAsync");
		if (pool.AcademicYearId != yearId || input.Offer.SeatPoolId != pool.Id) throw new BusinessException("AdmissionCrm:WaitlistSeatPoolMismatch");
		var leads = await leadRepository.GetQueryableAsync();
		var scoped = await scopeAccess.ApplyCurrentUserAdmissionLeadScopeAsync(leads.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId));
		var leadIds = await AsyncExecuter.ToListAsync(scoped.Select(x => x.Id));
		var queryable = await waitlistRepository.GetQueryableAsync();
		var entries = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id &&
			x.SeatPoolId == pool.Id && leadIds.Contains(x.LeadId) && x.Status == AdmissionCrmWaitlistStatus.Active && !x.PromotionPaused));
		var offersQuery = await offerRepository.GetQueryableAsync();
		var openLeadIds = (await AsyncExecuter.ToListAsync(offersQuery.Where(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == yearId && AdmissionCrmFinalAdmissionState.OpenOfferStatuses.Contains(x.Status))))
			.Select(x => x.LeadId).ToHashSet();
		var candidates = entries.Where(x => !openLeadIds.Contains(x.LeadId)).OrderByDescending(x => x.PriorityScore)
			.ThenBy(x => x.PositionSequence).Take(Math.Min(input.MaximumOffers, pool.RemainingCapacity)).ToList();
		var offerIds = new List<Guid>();
		var failedIds = new List<Guid>();
		foreach (var entry in candidates)
		{
			try
			{
				var offer = await offers.CreateAsync(entry.LeadId, input.Offer);
				offerIds.Add(offer.Id);
				await history.AddAsync(entry, AdmissionCrmWaitlistHistoryAction.OfferPrepared,
					"Automated promotion prepared a draft offer for staff approval.", offer.Id);
			}
			catch (Exception exception) when (exception is BusinessException or AbpAuthorizationException)
			{
				failedIds.Add(entry.Id);
				Logger.LogError(exception, "Failed to prepare waitlist promotion for entry {EntryId}.", entry.Id);
			}
		}
		return new AdmissionCrmWaitlistPromotionRunResultDto { SeatPoolId = pool.Id, EvaluatedCount = entries.Count,
			PreparedCount = offerIds.Count, OfferIds = offerIds, FailedEntryIds = failedIds };
	}
}
