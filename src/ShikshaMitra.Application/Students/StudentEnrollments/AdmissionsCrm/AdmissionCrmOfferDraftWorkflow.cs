using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmOfferDraftWorkflow(
	IRepository<AdmissionCrmSeatOffer, Guid> offerRepository,
	IRepository<AdmissionInquiryLead, Guid> leadRepository,
	StudentScopeAccessService scopeAccess,
	AdmissionCrmFinalAdmissionAccess access,
	AdmissionCrmOfferReadinessWorkflow readiness,
	AdmissionCrmFinalAdmissionMapper mapper,
	AdmissionCrmFinalAdmissionTimeline timeline) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmSeatOfferPreviewDto> PreviewAsync(Guid leadId, CreateAdmissionCrmSeatOfferDto input)
	{
		var lead = await access.GetLeadAsync(leadId, "PreviewOfferAsync");
		var pool = await access.GetPoolAsync(input.SeatPoolId, "PreviewOfferAsync");
		var blockers = new List<string>();
		if (pool.AcademicYearId != lead.AcademicYearId) blockers.Add("AdmissionCrm:SeatPoolAcademicYearMismatch");
		if (input.PlaybookId.HasValue && input.PlaybookId != pool.PlaybookId) blockers.Add("AdmissionCrm:SeatOfferPlaybookMismatch");
		if (pool.Status != AdmissionCrmSeatPoolStatus.Active) blockers.Add("AdmissionCrm:SeatPoolClosed");
		if (pool.RemainingCapacity <= 0) blockers.Add("AdmissionCrm:SeatPoolCapacityExhausted");
		try { await readiness.EnsureOfferAsync(lead, input.RegistrationApplicationId); }
		catch (BusinessException exception) { blockers.Add(exception.Code ?? "AdmissionCrm:SeatOfferReadinessFailed"); }
		var queryable = await offerRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id && x.LeadId == leadId &&
			AdmissionCrmFinalAdmissionState.OpenOfferStatuses.Contains(x.Status)))) blockers.Add("AdmissionCrm:LeadAlreadyHasOpenOffer");
		try
		{
			_ = new AdmissionCrmSeatOffer(GuidGenerator.Create(), CurrentTenant.Id, lead.AcademicYearId, leadId, pool.Id,
				input.RegistrationApplicationId, pool.PlaybookId, null, "PREVIEW-ONLY", 1, input.OfferType,
				JsonSerializer.Serialize(input.Terms), input.Language, input.ValidFrom, input.ValidUntil);
		}
		catch (BusinessException exception) { blockers.Add(exception.Code ?? "AdmissionCrm:InvalidSeatOffer"); }
		return new AdmissionCrmSeatOfferPreviewDto { LeadId = leadId, SeatPoolId = pool.Id,
			RemainingCapacity = pool.RemainingCapacity, BlockingReasons = blockers.Distinct().ToList(), IsReady = blockers.Count == 0 };
	}

	public async Task<AdmissionCrmSeatOfferDto> CreateAsync(Guid leadId, CreateAdmissionCrmSeatOfferDto input)
	{
		var lead = await access.GetLeadAsync(leadId, "CreateOfferAsync");
		var pool = await access.GetPoolAsync(input.SeatPoolId, "CreateOfferAsync");
		if (pool.AcademicYearId != lead.AcademicYearId) throw new BusinessException("AdmissionCrm:SeatPoolAcademicYearMismatch");
		if (input.PlaybookId.HasValue && input.PlaybookId != pool.PlaybookId) throw new BusinessException("AdmissionCrm:SeatOfferPlaybookMismatch");
		await readiness.EnsureOfferAsync(lead, input.RegistrationApplicationId);
		var queryable = await offerRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id && x.LeadId == leadId &&
			AdmissionCrmFinalAdmissionState.OpenOfferStatuses.Contains(x.Status))))
			throw new BusinessException("AdmissionCrm:LeadAlreadyHasOpenOffer");
		var prior = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id && x.LeadId == leadId)
			.OrderByDescending(x => x.Version));
		var id = GuidGenerator.Create();
		var offer = new AdmissionCrmSeatOffer(id, CurrentTenant.Id, lead.AcademicYearId, leadId, pool.Id,
			input.RegistrationApplicationId, pool.PlaybookId, prior.FirstOrDefault()?.Id,
			$"OFF-{Clock.Now:yyyyMMdd}-{id:N}".ToUpperInvariant(), prior.Count == 0 ? 1 : prior[0].Version + 1,
			input.OfferType, JsonSerializer.Serialize(input.Terms), input.Language, input.ValidFrom, input.ValidUntil);
		await offerRepository.InsertAsync(offer, autoSave: true);
		await timeline.AddAsync(lead, AdmissionCrmTimelineEventType.OfferCreated, "Seat offer created",
			$"Offer {offer.OfferNumber} version {offer.Version} was created.", offer.Id);
		return await mapper.OfferAsync(offer);
	}

	public async Task<PagedResultDto<AdmissionCrmSeatOfferDto>> GetListAsync(Guid yearId, GetAdmissionCrmSeatOfferListInput input)
	{
		await scopeAccess.EnsureCanAccessAcademicYearAsync(yearId, "GetOffersAsync");
		var leads = await leadRepository.GetQueryableAsync();
		var scoped = await scopeAccess.ApplyCurrentUserAdmissionLeadScopeAsync(leads.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId));
		var leadIds = scoped.Select(x => x.Id);
		var queryable = await offerRepository.GetQueryableAsync();
		var query = queryable.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId && leadIds.Contains(x.LeadId));
		if (input.LeadId.HasValue) query = query.Where(x => x.LeadId == input.LeadId.Value);
		if (input.SeatPoolId.HasValue) query = query.Where(x => x.SeatPoolId == input.SeatPoolId.Value);
		if (input.Status.HasValue) query = query.Where(x => x.Status == input.Status.Value);
		var count = await AsyncExecuter.CountAsync(query);
		var offers = await AsyncExecuter.ToListAsync(query.OrderByDescending(x => x.CreationTime).Skip(input.SkipCount).Take(input.MaxResultCount));
		var results = new List<AdmissionCrmSeatOfferDto>();
		foreach (var offer in offers) results.Add(await mapper.OfferAsync(offer));
		return new PagedResultDto<AdmissionCrmSeatOfferDto>(count, results);
	}

	public async Task<AdmissionCrmSeatOfferDto> GetAsync(Guid id) => await mapper.OfferAsync(await access.GetOfferAsync(id, "GetOfferAsync"));
}
