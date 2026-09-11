using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionSeatReservation : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid PoolId { get; private set; }
	public Guid ApplicationId { get; private set; }
	public Guid StageInstanceId { get; private set; }
	public AdmissionSeatReservationStatus Status { get; private set; }
	public DateTime? ReservedAt { get; private set; }
	public DateTime? ExpiresAt { get; private set; }
	public DateTime? ConfirmedAt { get; private set; }
	public DateTime? ReleasedAt { get; private set; }
	public int RenewalCount { get; private set; }
	public Guid? LastActorId { get; private set; }
	public string? LastReason { get; private set; }

	protected AdmissionSeatReservation() { }

	public AdmissionSeatReservation(
		Guid id,
		Guid? tenantId,
		Guid poolId,
		Guid applicationId,
		Guid stageInstanceId,
		AdmissionSeatReservationStatus status,
		DateTime now,
		DateTime? expiresAt,
		Guid? actorId,
		string? reason) : base(id)
	{
		TenantId = tenantId;
		PoolId = Check.NotDefaultOrNull<Guid>(poolId, nameof(poolId));
		ApplicationId = Check.NotDefaultOrNull<Guid>(applicationId, nameof(applicationId));
		StageInstanceId = Check.NotDefaultOrNull<Guid>(stageInstanceId, nameof(stageInstanceId));
		if (status == AdmissionSeatReservationStatus.Reserved) Reserve(now, expiresAt, actorId, reason);
		else if (status == AdmissionSeatReservationStatus.Waitlisted) Waitlist(actorId, reason);
		else throw Invalid("A seat record must begin as reserved or waitlisted.");
	}

	public void Reserve(DateTime now, DateTime? expiresAt, Guid? actorId, string? reason)
	{
		if (Status == AdmissionSeatReservationStatus.Confirmed)
			throw Invalid("A confirmed seat cannot be reserved again.");
		if (!expiresAt.HasValue || expiresAt.Value <= now)
			throw new BusinessException(AdmissionSeatStageErrorCodes.DurationInvalid);
		Status = AdmissionSeatReservationStatus.Reserved;
		ReservedAt = now;
		ExpiresAt = expiresAt;
		ConfirmedAt = null;
		ReleasedAt = null;
		RenewalCount = 0;
		SetAudit(actorId, reason);
	}

	public void Confirm(DateTime now, Guid? actorId, string? reason)
	{
		if (Status != AdmissionSeatReservationStatus.Reserved || ExpiresAt <= now)
			throw Invalid("Only a current seat reservation can be confirmed.");
		Status = AdmissionSeatReservationStatus.Confirmed;
		ConfirmedAt = now;
		ExpiresAt = null;
		SetAudit(actorId, reason);
	}

	public void Renew(DateTime now, DateTime expiresAt, int maximumRenewals, Guid? actorId, string? reason)
	{
		if (Status != AdmissionSeatReservationStatus.Reserved || ExpiresAt <= now)
			throw Invalid("Only a current seat reservation can be renewed.");
		if (RenewalCount >= maximumRenewals)
			throw new BusinessException(AdmissionSeatStageErrorCodes.RenewalNotAllowed);
		if (expiresAt <= now || expiresAt <= ExpiresAt)
			throw new BusinessException(AdmissionSeatStageErrorCodes.DurationInvalid);
		ExpiresAt = expiresAt;
		RenewalCount++;
		SetAudit(actorId, reason);
	}

	public void Waitlist(Guid? actorId, string? reason)
	{
		if (Status == AdmissionSeatReservationStatus.Confirmed)
			throw Invalid("A confirmed seat cannot be moved to the waitlist.");
		Status = AdmissionSeatReservationStatus.Waitlisted;
		ReservedAt = null;
		ExpiresAt = null;
		ConfirmedAt = null;
		ReleasedAt = null;
		RenewalCount = 0;
		SetAudit(actorId, reason);
	}

	public void Release(DateTime now, Guid? actorId, string? reason)
	{
		if (Status is AdmissionSeatReservationStatus.Released or AdmissionSeatReservationStatus.Expired)
			throw Invalid("This seat is already released or expired.");
		Status = AdmissionSeatReservationStatus.Released;
		ExpiresAt = null;
		ReleasedAt = now;
		SetAudit(actorId, reason);
	}

	public void Expire(DateTime now)
	{
		if (Status != AdmissionSeatReservationStatus.Reserved || ExpiresAt > now)
			throw Invalid("Only an elapsed reservation can expire.");
		Status = AdmissionSeatReservationStatus.Expired;
		ReleasedAt = now;
		SetAudit(null, "Reservation expired.");
	}

	private void SetAudit(Guid? actorId, string? reason)
	{
		LastActorId = actorId;
		LastReason = string.IsNullOrWhiteSpace(reason) ? null : Check.Length(reason.Trim(), nameof(reason), 512);
	}

	private static BusinessException Invalid(string message) =>
		new(AdmissionSeatStageErrorCodes.InvalidState, message);
}
