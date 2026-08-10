using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmOfferReservationWorkflow(
	IRepository<AdmissionCrmSeatReservation, Guid> reservationRepository,
	IRepository<AdmissionCrmSeatPool, Guid> poolRepository,
	IRepository<AdmissionCrmSeatOffer, Guid> offerRepository,
	IRepository<AdmissionCrmWaitlistEntry, Guid> waitlistRepository,
	AdmissionCrmFinalAdmissionAccess access,
	AdmissionCrmFinalAdmissionTimeline timeline,
	AdmissionCrmWaitlistHistoryWorkflow history) : ApplicationService, ITransientDependency
{
	public async Task IssueAsync(AdmissionCrmSeatOffer offer, IssueAdmissionCrmSeatOfferDto input)
	{
		if (input.CapacityOverride && !await AuthorizationService.IsGrantedAsync(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.OverrideSeatCapacity))
			throw new AbpAuthorizationException("Seat-capacity override permission is required.");
		var pool = await access.GetPoolAsync(offer.SeatPoolId, "IssueOfferAsync");
		var reservations = await reservationRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(reservations.Where(x => x.TenantId == CurrentTenant.Id && x.SeatOfferId == offer.Id)))
			throw new BusinessException("AdmissionCrm:SeatOfferAlreadyHasReservation");
		var overrideUsed = pool.ReserveSeat(input.CapacityOverride, input.CapacityOverrideReason);
		var reservation = new AdmissionCrmSeatReservation(GuidGenerator.Create(), CurrentTenant.Id, offer.AcademicYearId,
			offer.LeadId, pool.Id, offer.Id, Clock.Now, offer.ValidUntil, overrideUsed, input.CapacityOverrideReason);
		offer.Issue(reservation.Id, Clock.Now);
		await poolRepository.UpdateAsync(pool, autoSave: true);
		await reservationRepository.InsertAsync(reservation, autoSave: true);
		await offerRepository.UpdateAsync(offer, autoSave: true);
		var waitlist = await waitlistRepository.GetQueryableAsync();
		var entry = await AsyncExecuter.FirstOrDefaultAsync(waitlist.Where(x => x.TenantId == CurrentTenant.Id &&
			x.LeadId == offer.LeadId && x.SeatPoolId == pool.Id && x.Status == AdmissionCrmWaitlistStatus.Active));
		if (entry != null)
		{
			entry.Promote(offer.Id, Clock.Now);
			await history.AddAsync(entry, AdmissionCrmWaitlistHistoryAction.Promoted,
				"Offer issued and the waitlist entry was promoted.", offer.Id);
			pool.RemoveWaitlistedApplicant();
			await waitlistRepository.UpdateAsync(entry, autoSave: true);
			await poolRepository.UpdateAsync(pool, autoSave: true);
			await timeline.AddForOfferAsync(offer, AdmissionCrmTimelineEventType.WaitlistPromoted,
				"Waitlist entry promoted", $"Waitlist entry was promoted through offer {offer.OfferNumber}.");
		}
		await timeline.AddForOfferAsync(offer, AdmissionCrmTimelineEventType.SeatReserved,
			"Seat reserved", $"Seat pool {pool.Code} reserved a seat for offer {offer.OfferNumber}.");
		await timeline.AddForOfferAsync(offer, AdmissionCrmTimelineEventType.OfferIssued,
			"Seat offer issued", $"Offer {offer.OfferNumber} is valid until {offer.ValidUntil:O}.");
	}

	public async Task AcceptAsync(AdmissionCrmSeatOffer offer, RespondAdmissionCrmSeatOfferDto input)
	{
		var reservation = await access.GetReservationAsync(offer.Id);
		var pool = await access.GetPoolAsync(offer.SeatPoolId, "AcceptOfferAsync");
		offer.Accept(input.ResponseSource, Clock.Now, JsonSerializer.Serialize(new
		{
			input.Language, input.Declarations, input.EvidenceReference, ActorUserId = CurrentUser.Id,
			RecordedAt = Clock.Now, TermsVersion = offer.Version
		}));
		reservation.Commit(Clock.Now);
		pool.AcceptReservedSeat();
		await offerRepository.UpdateAsync(offer, autoSave: true);
		await reservationRepository.UpdateAsync(reservation, autoSave: true);
		await poolRepository.UpdateAsync(pool, autoSave: true);
		var queryable = await waitlistRepository.GetQueryableAsync();
		var entry = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id &&
			x.OfferId == offer.Id && x.Status == AdmissionCrmWaitlistStatus.Promoted));
		if (entry != null)
		{
			entry.MarkAccepted(Clock.Now);
			await waitlistRepository.UpdateAsync(entry, autoSave: true);
		}
	}

	public async Task ReleaseAsync(AdmissionCrmSeatOffer offer, string reason, bool cancelled)
	{
		if (!offer.ReservationId.HasValue) return;
		var reservation = await access.GetReservationAsync(offer.Id);
		if (reservation.Status is AdmissionCrmSeatReservationStatus.Released or AdmissionCrmSeatReservationStatus.Cancelled or
			AdmissionCrmSeatReservationStatus.Expired) return;
		var pool = await access.GetPoolAsync(offer.SeatPoolId, "ReleaseReservationAsync");
		var accepted = reservation.Status == AdmissionCrmSeatReservationStatus.Committed;
		reservation.Release(reason, Clock.Now, cancelled);
		pool.ReleaseSeat(accepted, cancelled);
		await reservationRepository.UpdateAsync(reservation, autoSave: true);
		await poolRepository.UpdateAsync(pool, autoSave: true);
		await timeline.AddForOfferAsync(offer, AdmissionCrmTimelineEventType.SeatReleased, "Seat released", reason);
	}

	public async Task ClosePromotedWaitlistAsync(AdmissionCrmSeatOffer offer, AdmissionCrmPromotedWaitlistClosure closure)
	{
		var queryable = await waitlistRepository.GetQueryableAsync();
		var entry = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id &&
			x.OfferId == offer.Id && x.Status == AdmissionCrmWaitlistStatus.Promoted));
		if (entry == null) return;
		if (closure == AdmissionCrmPromotedWaitlistClosure.Reactivated)
		{
			entry.ReactivatePromotedOffer();
			var pool = await access.GetPoolAsync(entry.SeatPoolId, "UpdatePromotedWaitlistAfterOfferClosureAsync");
			pool.AddWaitlistedApplicant();
			await poolRepository.UpdateAsync(pool, autoSave: true);
		}
		else if (closure == AdmissionCrmPromotedWaitlistClosure.Expired) entry.ExpirePromotedOffer(Clock.Now);
		else entry.ClosePromotedOffer(offer.ResponseReason ?? "Offer declined.", Clock.Now);
		await waitlistRepository.UpdateAsync(entry, autoSave: true);
	}

	public async Task ExpireAsync(AdmissionCrmSeatOffer offer)
	{
		offer.Expire(Clock.Now);
		var reservation = await access.GetReservationAsync(offer.Id);
		var pool = await access.GetPoolAsync(offer.SeatPoolId, "ExpireOfferAsync");
		reservation.Expire(Clock.Now);
		pool.ReleaseSeat(false, false);
		await reservationRepository.UpdateAsync(reservation, autoSave: true);
		await poolRepository.UpdateAsync(pool, autoSave: true);
		await offerRepository.UpdateAsync(offer, autoSave: true);
		await ClosePromotedWaitlistAsync(offer, AdmissionCrmPromotedWaitlistClosure.Expired);
		await timeline.AddForOfferAsync(offer, AdmissionCrmTimelineEventType.OfferExpired,
			"Seat offer expired", $"Offer {offer.OfferNumber} expired and its seat was released.");
	}
}

public enum AdmissionCrmPromotedWaitlistClosure { Declined, Reactivated, Expired }
