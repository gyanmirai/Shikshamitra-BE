using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionSeatReadService(
	IRepository<AdmissionSeatPool, Guid> pools,
	IRepository<AdmissionSeatReservation, Guid> reservations,
	IRepository<AdmissionApplicationDecisionHistory, Guid> history,
	AdmissionSeatAccessService access) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionSeatStageDto> GetAsync(Guid applicationId, Guid stageId)
	{
		var context = await access.GetContextAsync(applicationId, stageId);
		var policy = context.Policy;
		var pool = await pools.FindAsync(item => item.AdmissionOfferingId == context.Application.AdmissionOfferingId);
		var reservation = pool == null ? null : await reservations.FindAsync(item =>
			item.ApplicationId == applicationId && item.StageInstanceId == stageId);
		var poolReservations = pool == null ? [] : await reservations.GetListAsync(item => item.PoolId == pool.Id);
		var now = Clock.Now;
		var reservedCount = poolReservations.Count(item => item.Status == AdmissionSeatReservationStatus.Reserved && item.ExpiresAt > now);
		var confirmedCount = poolReservations.Count(item => item.Status == AdmissionSeatReservationStatus.Confirmed);
		var capacity = pool?.Capacity ?? policy?.DefaultCapacity ?? 0;
		var remaining = Math.Max(0, capacity - reservedCount - confirmedCount);
		var status = Status(reservation, now);
		var waitlisted = poolReservations.Where(item => item.Status == AdmissionSeatReservationStatus.Waitlisted)
			.OrderBy(item => item.CreationTime).ThenBy(item => item.Id).ToList();
		var waitlistPosition = reservation?.Status == AdmissionSeatReservationStatus.Waitlisted
			? waitlisted.FindIndex(item => item.Id == reservation.Id) + 1 : (int?)null;
		var events = (await history.GetListAsync(item => item.ApplicationId == applicationId &&
			item.StageInstanceId == stageId && item.Action != null && item.Action.StartsWith("seat-")))
			.OrderBy(item => item.OccurredAt).ToList();
		var canComplete = policy?.AllowedCompletionOutcomes.Contains(status, StringComparer.Ordinal) == true;
		return new AdmissionSeatStageDto
		{
			ApplicationId = applicationId, AdmissionOfferingId = context.Application.AdmissionOfferingId,
			StageId = stageId, SeatStageEnabled = policy != null, Policy = policy, IsActive = context.IsActive,
			Status = status, Capacity = capacity, ReservedCount = reservedCount, ConfirmedCount = confirmedCount,
			RemainingCapacity = remaining, IsOverCapacity = reservedCount + confirmedCount > capacity,
			ReservationId = reservation?.Id, ReservedAt = reservation?.ReservedAt,
			ExpiresAt = status == "reserved" ? reservation?.ExpiresAt : null,
			ConfirmedAt = reservation?.ConfirmedAt, RenewalCount = reservation?.RenewalCount ?? 0,
			WaitlistPosition = waitlistPosition, ConcurrencyStamp = context.Application.ConcurrencyStamp,
			PoolConcurrencyStamp = pool?.ConcurrencyStamp, ReservationConcurrencyStamp = reservation?.ConcurrencyStamp,
			Events = events.Select(Map).ToList(), Blockers = Blockers(policy, status),
			CanReserve = context.IsActive && policy != null && status is "ready" or "released" or "expired" or "waitlisted" &&
				(remaining > 0 || policy.AllowCapacityOverride),
			CanConfirm = context.IsActive && policy != null && status == "reserved",
			CanRenew = context.IsActive && policy?.AllowRenewal == true && status == "reserved" &&
				reservation!.RenewalCount < policy.MaximumRenewals,
			CanRelease = policy != null && status is "reserved" or "confirmed" or "waitlisted" &&
				(context.IsActive || status == "confirmed") && context.Application.Status != AdmissionApplicationStatus.AdmissionFinalized,
			CanJoinWaitlist = context.IsActive && policy?.AllowWaitlist == true && remaining == 0 &&
				status is "ready" or "released" or "expired",
			CanManageCapacity = context.IsActive && policy?.AllowCapacityOverride == true,
			CanComplete = canComplete
		};
	}

	public static string Status(AdmissionSeatReservation? reservation, DateTime now)
	{
		if (reservation == null) return "ready";
		if (reservation.Status == AdmissionSeatReservationStatus.Reserved && reservation.ExpiresAt <= now) return "expired";
		return reservation.Status.ToString().ToLowerInvariant();
	}

	private static List<string> Blockers(AdmissionSeatStagePolicy? policy, string status)
	{
		if (policy == null) return [];
		if (policy.AllowedCompletionOutcomes.Contains(status, StringComparer.Ordinal)) return [];
		return status switch
		{
			"ready" => ["Reserve a seat or place the applicant on the waitlist."],
			"reserved" => ["Confirm the reservation before it expires."],
			"expired" => ["Reserve another seat or allow expiry as a completion outcome."],
			"released" => ["Reserve another seat or allow release as a completion outcome."],
			"waitlisted" => ["Reserve a seat or allow waitlisting as a completion outcome."],
			_ => [$"The {status} outcome is not configured to complete this stage."]
		};
	}

	private static AdmissionSeatEventDto Map(AdmissionApplicationDecisionHistory item) => new()
	{
		Id = item.Id, Action = item.Action!, Reason = item.Reason, ActorId = item.ActorId,
		OccurredAt = item.OccurredAt, MetadataJson = item.MetadataJson
	};
}
