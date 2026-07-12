using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmSeatReservation : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid LeadId { get; private set; }
	public Guid SeatPoolId { get; private set; }
	public Guid SeatOfferId { get; private set; }
	public AdmissionCrmSeatReservationStatus Status { get; private set; }
	public DateTime ReservedAt { get; private set; }
	public DateTime ExpiresAt { get; private set; }
	public DateTime? CommittedAt { get; private set; }
	public DateTime? ReleasedAt { get; private set; }
	public string? ReleaseReason { get; private set; }
	public bool CapacityOverrideUsed { get; private set; }
	public string? CapacityOverrideReason { get; private set; }

	protected AdmissionCrmSeatReservation()
	{
	}

	public AdmissionCrmSeatReservation(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		Guid leadId,
		Guid seatPoolId,
		Guid seatOfferId,
		DateTime reservedAt,
		DateTime expiresAt,
		bool capacityOverrideUsed,
		string? capacityOverrideReason) : base(id)
	{
		var normalizedReservedAt = reservedAt.ToUniversalTime();
		var normalizedExpiresAt = expiresAt.ToUniversalTime();
		if (normalizedExpiresAt <= normalizedReservedAt)
		{
			throw new BusinessException("AdmissionCrm:InvalidSeatReservationExpiry");
		}
		if (capacityOverrideUsed && string.IsNullOrWhiteSpace(capacityOverrideReason))
		{
			throw new BusinessException("AdmissionCrm:SeatCapacityOverrideReasonRequired");
		}

		TenantId = tenantId;
		AcademicYearId = academicYearId;
		LeadId = leadId;
		SeatPoolId = seatPoolId;
		SeatOfferId = seatOfferId;
		ReservedAt = normalizedReservedAt;
		ExpiresAt = normalizedExpiresAt;
		Status = AdmissionCrmSeatReservationStatus.Active;
		CapacityOverrideUsed = capacityOverrideUsed;
		CapacityOverrideReason = Normalize(capacityOverrideReason);
	}

	public void Commit(DateTime committedAt)
	{
		EnsureActive(committedAt);
		Status = AdmissionCrmSeatReservationStatus.Committed;
		CommittedAt = committedAt.ToUniversalTime();
	}

	public void Release(string reason, DateTime releasedAt, bool cancelled = false)
	{
		if (Status is AdmissionCrmSeatReservationStatus.Released or AdmissionCrmSeatReservationStatus.Cancelled)
		{
			return;
		}
		if (Status == AdmissionCrmSeatReservationStatus.Expired)
		{
			throw new BusinessException("AdmissionCrm:ExpiredSeatReservationCannotBeReleased");
		}

		Status = cancelled ? AdmissionCrmSeatReservationStatus.Cancelled : AdmissionCrmSeatReservationStatus.Released;
		ReleasedAt = releasedAt.ToUniversalTime();
		ReleaseReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), AdmissionCrmConsts.MaxNotesLength);
	}

	public void Expire(DateTime expiredAt)
	{
		if (Status != AdmissionCrmSeatReservationStatus.Active)
		{
			return;
		}
		var normalizedExpiredAt = expiredAt.ToUniversalTime();
		if (normalizedExpiredAt < ExpiresAt)
		{
			throw new BusinessException("AdmissionCrm:SeatReservationNotExpired");
		}

		Status = AdmissionCrmSeatReservationStatus.Expired;
		ReleasedAt = normalizedExpiredAt;
		ReleaseReason = "Reservation expired.";
	}

	private void EnsureActive(DateTime at)
	{
		if (Status != AdmissionCrmSeatReservationStatus.Active)
		{
			throw new BusinessException("AdmissionCrm:SeatReservationNotActive");
		}
		if (at.ToUniversalTime() > ExpiresAt)
		{
			throw new BusinessException("AdmissionCrm:SeatReservationExpired");
		}
	}

	private static string? Normalize(string? value) =>
		string.IsNullOrWhiteSpace(value) ? null : Check.Length(value.Trim(), nameof(value), AdmissionCrmConsts.MaxNotesLength);
}
