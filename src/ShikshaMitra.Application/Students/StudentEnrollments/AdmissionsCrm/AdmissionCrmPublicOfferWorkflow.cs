using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPublicOfferWorkflow(
	AdmissionCrmPublicAccess access,
	IRepository<AdmissionCrmSeatOffer, Guid> offerRepository,
	IRepository<AdmissionCrmSeatReservation, Guid> reservationRepository,
	IRepository<AdmissionCrmSeatPool, Guid> poolRepository,
	IRepository<AdmissionCrmWaitlistEntry, Guid> waitlistRepository,
	IRepository<AdmissionCrmAdmissionQuotation, Guid> quotationRepository,
	IRepository<AdmissionCrmTimelineEvent, Guid> timelineRepository) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionApplicantStatusOfferDto> RespondAsync(
		Guid tenantId,
		string token,
		Guid offerId,
		RespondAdmissionApplicantOfferDto input)
	{
		using (CurrentTenant.Change(tenantId))
		using (var uow = UnitOfWorkManager.Begin(
			new AbpUnitOfWorkOptions { IsTransactional = true }, requiresNew: true))
		{
			var (statusAccess, lead) = await access.GetAsync(token);
			await access.EnsureAccessSessionAsync(statusAccess, lead, input.AccessSessionToken);
			var offer = await AsyncExecuter.FirstOrDefaultAsync((await offerRepository.GetQueryableAsync()).Where(item =>
				item.Id == offerId && item.TenantId == tenantId && item.LeadId == lead.Id)) ??
				throw new BusinessException("AdmissionCrm:SeatOfferNotFound");
			var reservation = await AsyncExecuter.FirstOrDefaultAsync(
				(await reservationRepository.GetQueryableAsync()).Where(item =>
					item.TenantId == tenantId && item.SeatOfferId == offer.Id)) ??
				throw new BusinessException("AdmissionCrm:SeatReservationNotFound");
			var pool = await AsyncExecuter.FirstOrDefaultAsync((await poolRepository.GetQueryableAsync()).Where(item =>
				item.Id == offer.SeatPoolId && item.TenantId == tenantId)) ??
				throw new BusinessException("AdmissionCrm:SeatPoolNotFound");

			var now = Clock.Now;
			if (input.Accept)
			{
				if (!input.Declarations.TryGetValue("offerTermsAccepted", out var termsAccepted) || !termsAccepted)
				{
					throw new BusinessException("AdmissionCrm:OfferTermsAcceptanceRequired");
				}

				await EnsurePaymentGateAsync(lead.Id, now);
				offer.Accept(
					"Applicant OTP link",
					now,
					JsonSerializer.Serialize(new
					{
						input.Language,
						input.Declarations,
						Actor = "ApplicantOtpSession",
						RespondedAt = now,
						TermsVersion = offer.Version
					}));
				reservation.Commit(now);
				pool.AcceptReservedSeat();
				await UpdatePromotedWaitlistAsync(offer, now, accepted: true, null);
			}
			else
			{
				var reason = string.IsNullOrWhiteSpace(input.Reason)
					? "Applicant declined the offer."
					: input.Reason.Trim();
				offer.Decline(
					"Applicant OTP link",
					reason,
					now,
					JsonSerializer.Serialize(new
					{
						input.Language,
						Actor = "ApplicantOtpSession",
						RespondedAt = now,
						TermsVersion = offer.Version
					}));
				reservation.Release(reason, now);
				pool.ReleaseSeat(wasAccepted: false, cancelled: false);
				await UpdatePromotedWaitlistAsync(offer, now, accepted: false, reason);
			}

			await offerRepository.UpdateAsync(offer, autoSave: true);
			await reservationRepository.UpdateAsync(reservation, autoSave: true);
			await poolRepository.UpdateAsync(pool, autoSave: true);
			await timelineRepository.InsertAsync(
				new AdmissionCrmTimelineEvent(
					GuidGenerator.Create(), tenantId, lead.Id, lead.AcademicYearId,
					input.Accept ? AdmissionCrmTimelineEventType.OfferAccepted : AdmissionCrmTimelineEventType.OfferDeclined,
					input.Accept ? "Seat offer accepted" : "Seat offer declined",
					input.Accept
						? $"Offer {offer.OfferNumber} was accepted through the applicant status link."
						: offer.ResponseReason,
					offer.Id, null, now),
				autoSave: true);
			await uow.CompleteAsync();
			return ToDto(offer);
		}
	}

	public async Task<AdmissionCrmSeatOffer?> GetOfferOrDefaultAsync(Guid leadId)
	{
		var offers = await AsyncExecuter.ToListAsync((await offerRepository.GetQueryableAsync()).Where(offer =>
			offer.TenantId == CurrentTenant.Id && offer.LeadId == leadId));
		return offers.OrderByDescending(offer => offer.Version).FirstOrDefault(offer =>
			offer.Status is AdmissionCrmSeatOfferStatus.Issued or AdmissionCrmSeatOfferStatus.Opened or AdmissionCrmSeatOfferStatus.Accepted) ??
			offers.OrderByDescending(offer => offer.Version).FirstOrDefault();
	}

	public async Task OpenIfNeededAsync(AdmissionInquiryLead lead, AdmissionCrmSeatOffer? offer)
	{
		if (offer?.Status != AdmissionCrmSeatOfferStatus.Issued || !offer.MarkOpened(Clock.Now))
		{
			return;
		}

		await offerRepository.UpdateAsync(offer, autoSave: true);
		await timelineRepository.InsertAsync(
			new AdmissionCrmTimelineEvent(
				GuidGenerator.Create(), CurrentTenant.Id, lead.Id, lead.AcademicYearId,
				AdmissionCrmTimelineEventType.OfferOpened, "Seat offer opened",
				$"Offer {offer.OfferNumber} was opened through the applicant status link.",
				offer.Id, null, Clock.Now),
			autoSave: true);
	}

	private async Task EnsurePaymentGateAsync(Guid leadId, DateTime at)
	{
		var quotation = await AsyncExecuter.FirstOrDefaultAsync(
			(await quotationRepository.GetQueryableAsync())
				.Where(item => item.TenantId == CurrentTenant.Id && item.LeadId == leadId &&
					item.Status == AdmissionCrmQuotationStatus.Issued)
				.OrderByDescending(item => item.Version));
		if (quotation == null ||
			quotation.GateMilestone != AdmissionCrmPaymentGateMilestone.OfferAcceptance ||
			quotation.IsGateSatisfiedAt(at))
		{
			return;
		}

		throw new BusinessException("AdmissionCrm:PaymentGateNotSatisfied")
			.WithData("QuotationId", quotation.Id)
			.WithData("GateType", quotation.GateType)
			.WithData("FinanceState", quotation.FinanceState);
	}

	private async Task UpdatePromotedWaitlistAsync(
		AdmissionCrmSeatOffer offer,
		DateTime at,
		bool accepted,
		string? reason)
	{
		var entry = await AsyncExecuter.FirstOrDefaultAsync((await waitlistRepository.GetQueryableAsync()).Where(item =>
			item.TenantId == CurrentTenant.Id && item.OfferId == offer.Id &&
			item.Status == AdmissionCrmWaitlistStatus.Promoted));
		if (entry == null)
		{
			return;
		}

		if (accepted) entry.MarkAccepted(at);
		else entry.ClosePromotedOffer(reason ?? "Applicant declined the offer.", at);
		await waitlistRepository.UpdateAsync(entry, autoSave: true);
	}

	internal static AdmissionApplicantStatusOfferDto ToDto(AdmissionCrmSeatOffer offer) => new()
	{
		OfferId = offer.Id,
		OfferNumber = offer.OfferNumber,
		OfferType = offer.OfferType,
		Status = offer.Status,
		Terms = JsonSerializer.Deserialize<Dictionary<string, string?>>(
			offer.TermsSnapshotJson, AdmissionCrmPublicFormat.JsonOptions) ?? [],
		Language = offer.Language,
		ValidFrom = offer.ValidFrom,
		ValidUntil = offer.ValidUntil,
		RespondedAt = offer.RespondedAt,
		OpenedAt = offer.OpenedAt,
		ResponseReason = offer.ResponseReason
	};
}
