using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmOfferLifecycleWorkflow(
	IRepository<AdmissionCrmSeatOffer, Guid> offerRepository,
	AdmissionCrmFinalAdmissionAccess access,
	AdmissionCrmOfferReadinessWorkflow readiness,
	AdmissionCrmOfferReservationWorkflow reservations,
	AdmissionCrmFinalAdmissionMapper mapper,
	AdmissionCrmFinalAdmissionTimeline timeline) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmSeatOfferDto> SubmitAsync(Guid id)
	{
		var offer = await access.GetOfferAsync(id, "SubmitOfferAsync");
		offer.SubmitForApproval(CurrentUserId(), Clock.Now);
		await offerRepository.UpdateAsync(offer, autoSave: true);
		await timeline.AddForOfferAsync(offer, AdmissionCrmTimelineEventType.OfferSubmittedForApproval,
			"Seat offer submitted", $"Offer {offer.OfferNumber} was submitted for approval.");
		return await mapper.OfferAsync(offer);
	}

	public async Task<AdmissionCrmSeatOfferDto> ApproveAsync(Guid id)
	{
		var offer = await access.GetOfferAsync(id, "ApproveOfferAsync");
		offer.Approve(CurrentUserId(), Clock.Now);
		await offerRepository.UpdateAsync(offer, autoSave: true);
		await timeline.AddForOfferAsync(offer, AdmissionCrmTimelineEventType.OfferApproved,
			"Seat offer approved", $"Offer {offer.OfferNumber} was approved.");
		return await mapper.OfferAsync(offer);
	}

	public async Task<AdmissionCrmSeatOfferDto> IssueAsync(Guid id, IssueAdmissionCrmSeatOfferDto input)
	{
		var offer = await access.GetOfferAsync(id, "IssueOfferAsync");
		await reservations.IssueAsync(offer, input);
		return await mapper.OfferAsync(offer);
	}

	public async Task<AdmissionCrmSeatOfferDto> AcceptAsync(Guid id, RespondAdmissionCrmSeatOfferDto input)
	{
		var offer = await access.GetOfferAsync(id, "AcceptOfferAsync");
		await readiness.EnsurePaymentAsync(offer.LeadId, AdmissionCrmPaymentGateMilestone.OfferAcceptance);
		await reservations.AcceptAsync(offer, input);
		await timeline.AddForOfferAsync(offer, AdmissionCrmTimelineEventType.OfferAccepted,
			"Seat offer accepted", $"Offer {offer.OfferNumber} was accepted through {input.ResponseSource}.");
		return await mapper.OfferAsync(offer);
	}

	public async Task<AdmissionCrmSeatOfferDto> DeclineAsync(Guid id, RespondAdmissionCrmSeatOfferDto input)
	{
		var offer = await access.GetOfferAsync(id, "DeclineOfferAsync");
		offer.Decline(input.ResponseSource, input.Reason, Clock.Now, JsonSerializer.Serialize(new
		{
			input.Language, input.EvidenceReference, ActorUserId = CurrentUser.Id, RecordedAt = Clock.Now, TermsVersion = offer.Version
		}));
		await reservations.ReleaseAsync(offer, input.Reason ?? "Offer declined.", false);
		await reservations.ClosePromotedWaitlistAsync(offer, AdmissionCrmPromotedWaitlistClosure.Declined);
		await offerRepository.UpdateAsync(offer, autoSave: true);
		await timeline.AddForOfferAsync(offer, AdmissionCrmTimelineEventType.OfferDeclined,
			"Seat offer declined", input.Reason ?? $"Offer {offer.OfferNumber} was declined.");
		return await mapper.OfferAsync(offer);
	}

	public async Task<AdmissionCrmSeatOfferDto> RevokeAsync(Guid id, AdmissionCrmReasonDto input)
	{
		var offer = await access.GetOfferAsync(id, "RevokeOfferAsync");
		offer.Revoke(input.Reason, Clock.Now);
		await reservations.ReleaseAsync(offer, input.Reason, false);
		await reservations.ClosePromotedWaitlistAsync(offer, AdmissionCrmPromotedWaitlistClosure.Reactivated);
		await offerRepository.UpdateAsync(offer, autoSave: true);
		await timeline.AddForOfferAsync(offer, AdmissionCrmTimelineEventType.OfferRevoked, "Seat offer revoked", input.Reason);
		return await mapper.OfferAsync(offer);
	}

	public async Task<AdmissionCrmSeatOfferDto> SupersedeAsync(Guid id, SupersedeAdmissionCrmSeatOfferDto input)
	{
		var previous = await access.GetOfferAsync(id, "SupersedeOfferAsync");
		var lead = await access.GetLeadAsync(previous.LeadId, "SupersedeOfferAsync");
		var pool = await access.GetPoolAsync(input.SeatPoolId, "SupersedeOfferAsync");
		if (pool.AcademicYearId != previous.AcademicYearId || pool.AcademicYearId != lead.AcademicYearId)
			throw new BusinessException("AdmissionCrm:SeatPoolAcademicYearMismatch");
		if (input.PlaybookId.HasValue && input.PlaybookId != pool.PlaybookId) throw new BusinessException("AdmissionCrm:SeatOfferPlaybookMismatch");
		await readiness.EnsureOfferAsync(lead, input.RegistrationApplicationId);
		var queryable = await offerRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id && x.LeadId == previous.LeadId &&
			x.Id != previous.Id && AdmissionCrmFinalAdmissionState.OpenOfferStatuses.Contains(x.Status))))
			throw new BusinessException("AdmissionCrm:LeadAlreadyHasOpenOffer");
		var maxVersion = await AsyncExecuter.MaxAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id &&
			x.LeadId == previous.LeadId).Select(x => x.Version));
		var replacementId = GuidGenerator.Create();
		var replacement = new AdmissionCrmSeatOffer(replacementId, CurrentTenant.Id, previous.AcademicYearId,
			previous.LeadId, pool.Id, input.RegistrationApplicationId, pool.PlaybookId, previous.Id,
			$"OFF-{Clock.Now:yyyyMMdd}-{replacementId:N}".ToUpperInvariant(), maxVersion + 1, input.OfferType,
			JsonSerializer.Serialize(input.Terms), input.Language, input.ValidFrom, input.ValidUntil);
		if (previous.Status is AdmissionCrmSeatOfferStatus.Issued or AdmissionCrmSeatOfferStatus.Opened)
		{
			await reservations.ReleaseAsync(previous, $"Superseded: {input.Reason}", false);
			await reservations.ClosePromotedWaitlistAsync(previous, AdmissionCrmPromotedWaitlistClosure.Reactivated);
		}
		previous.MarkSuperseded(input.Reason, Clock.Now);
		await offerRepository.UpdateAsync(previous, autoSave: true);
		await offerRepository.InsertAsync(replacement, autoSave: true);
		await timeline.AddAsync(lead, AdmissionCrmTimelineEventType.OfferSuperseded, "Seat offer superseded",
			$"Offer {previous.OfferNumber} was superseded by {replacement.OfferNumber}. Reason: {input.Reason}", replacement.Id);
		return await mapper.OfferAsync(replacement);
	}

	public async Task<AdmissionCrmSeatOfferDto> ExpireAsync(Guid id) => await ExpireAsync(await access.GetOfferAsync(id, "ExpireOfferAsync"));

	public async Task<AdmissionCrmSeatOfferDto> ExpireAsync(AdmissionCrmSeatOffer offer)
	{
		await reservations.ExpireAsync(offer);
		return await mapper.OfferAsync(offer);
	}

	private Guid CurrentUserId() => CurrentUser.Id ?? throw new AbpAuthorizationException("An authenticated user is required.");
}
