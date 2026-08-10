using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmFinalAdmissionMapper(
	IRepository<AdmissionInquiryLead, Guid> leadRepository,
	StudentScopeAccessService scopeAccess,
	AdmissionCrmFinalAdmissionAccess access) : ApplicationService, ITransientDependency
{
	public static AdmissionCrmSeatPoolDto SeatPool(AdmissionCrmSeatPool pool) => new()
	{
		Id = pool.Id, AcademicYearId = pool.AcademicYearId, PlaybookId = pool.PlaybookId, Code = pool.Code, Name = pool.Name,
		RoundCode = pool.RoundCode, Campus = pool.Campus, DesiredClass = pool.DesiredClass, Board = pool.Board, Medium = pool.Medium,
		ApplicantCategory = pool.ApplicantCategory, OperationalTarget = pool.OperationalTarget, ApprovedCapacity = pool.ApprovedCapacity,
		ReservedSeats = pool.ReservedSeats, AcceptedSeats = pool.AcceptedSeats, ActivatedSeats = pool.ActivatedSeats,
		WaitlistedApplicants = pool.WaitlistedApplicants, ReleasedSeats = pool.ReleasedSeats, CancelledSeats = pool.CancelledSeats,
		OccupiedSeats = pool.OccupiedSeats, RemainingCapacity = pool.RemainingCapacity, IsOverCapacity = pool.IsOverCapacity,
		Status = pool.Status, ConcurrencyStamp = pool.ConcurrencyStamp
	};

	public async Task<AdmissionCrmSeatOfferDto> OfferAsync(AdmissionCrmSeatOffer offer)
	{
		await scopeAccess.EnsureCanAccessAdmissionLeadAsync(await leadRepository.GetAsync(offer.LeadId), "MapOfferAsync");
		AdmissionCrmSeatReservationDto? reservation = null;
		if (offer.ReservationId.HasValue) reservation = Reservation(await access.GetReservationAsync(offer.Id));
		return new AdmissionCrmSeatOfferDto
		{
			Id = offer.Id, AcademicYearId = offer.AcademicYearId, LeadId = offer.LeadId, SeatPoolId = offer.SeatPoolId,
			RegistrationApplicationId = offer.RegistrationApplicationId, PlaybookId = offer.PlaybookId,
			PreviousOfferId = offer.PreviousOfferId, ReservationId = offer.ReservationId, OfferNumber = offer.OfferNumber,
			Version = offer.Version, OfferType = offer.OfferType, Status = offer.Status,
			Terms = JsonSerializer.Deserialize<Dictionary<string, string?>>(offer.TermsSnapshotJson) ?? new(),
			Language = offer.Language, ValidFrom = offer.ValidFrom, ValidUntil = offer.ValidUntil,
			SubmittedByUserId = offer.SubmittedByUserId, SubmittedAt = offer.SubmittedAt,
			ApprovedByUserId = offer.ApprovedByUserId, ApprovedAt = offer.ApprovedAt, IssuedAt = offer.IssuedAt,
			OpenedAt = offer.OpenedAt, RespondedAt = offer.RespondedAt, ResponseSource = offer.ResponseSource,
			ResponseReason = offer.ResponseReason,
			ResponseSnapshot = JsonSerializer.Deserialize<Dictionary<string, object?>>(offer.ResponseSnapshotJson) ?? new(),
			Reservation = reservation
		};
	}

	public async Task<AdmissionCrmWaitlistEntryDto> WaitlistAsync(AdmissionCrmWaitlistEntry entry, int position)
	{
		await scopeAccess.EnsureCanAccessAdmissionLeadAsync(await leadRepository.GetAsync(entry.LeadId), "MapWaitlistEntryAsync");
		return new AdmissionCrmWaitlistEntryDto
		{
			Id = entry.Id, AcademicYearId = entry.AcademicYearId, LeadId = entry.LeadId, SeatPoolId = entry.SeatPoolId,
			PriorityScore = entry.PriorityScore, PositionSequence = entry.PositionSequence, CurrentPosition = position,
			Status = entry.Status, AddedAt = entry.AddedAt, PromotedAt = entry.PromotedAt, OfferId = entry.OfferId,
			ClosedAt = entry.ClosedAt, ClosureReason = entry.ClosureReason, IsProtected = entry.IsProtected,
			ProtectionReason = entry.ProtectionReason, PromotionPaused = entry.PromotionPaused,
			PromotionPauseReason = entry.PromotionPauseReason
		};
	}

	public static AdmissionCrmSeatReservationDto Reservation(AdmissionCrmSeatReservation value) => new()
	{
		Id = value.Id, LeadId = value.LeadId, SeatPoolId = value.SeatPoolId, SeatOfferId = value.SeatOfferId,
		Status = value.Status, ReservedAt = value.ReservedAt, ExpiresAt = value.ExpiresAt, CommittedAt = value.CommittedAt,
		ReleasedAt = value.ReleasedAt, ReleaseReason = value.ReleaseReason, CapacityOverrideUsed = value.CapacityOverrideUsed,
		CapacityOverrideReason = value.CapacityOverrideReason
	};

	public static AdmissionCrmWaitlistHistoryDto WaitlistHistory(AdmissionCrmWaitlistHistory value) => new()
	{
		Id = value.Id, WaitlistEntryId = value.WaitlistEntryId, Action = value.Action,
		PreviousPriorityScore = value.PreviousPriorityScore, PriorityScore = value.PriorityScore, Reason = value.Reason,
		RelatedOfferId = value.RelatedOfferId, ActorUserId = value.ActorUserId, OccurredAt = value.OccurredAt
	};

	public static AdmissionCrmAdmissionDecisionDto Decision(AdmissionCrmAdmissionDecision value) => new()
	{
		Id = value.Id, AcademicYearId = value.AcademicYearId, LeadId = value.LeadId, SeatPoolId = value.SeatPoolId,
		OfferId = value.OfferId, Status = value.Status, Reason = value.Reason, OverrideUsed = value.OverrideUsed,
		OverrideReason = value.OverrideReason, DecidedByUserId = value.DecidedByUserId, DecidedAt = value.DecidedAt
	};
}
