using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public class StudentSeatReservation : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid OnboardingCaseId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid StandardId { get; private set; }
	public Guid DivisionId { get; private set; }
	public int SeatNumber { get; private set; }
	public StudentSeatReservationStatus Status { get; private set; }
	public DateTime ReservedAt { get; private set; }
	public DateTime ExpiresAt { get; private set; }
	public DateTime? CommittedAt { get; private set; }
	public DateTime? ReleasedAt { get; private set; }
	public string? ReleaseReason { get; private set; }

	protected StudentSeatReservation() { }

	public StudentSeatReservation(Guid id, Guid? tenantId, Guid onboardingCaseId, Guid academicYearId,
		Guid standardId, Guid divisionId, int seatNumber, DateTime reservedAt, DateTime expiresAt) : base(id)
	{
		if (expiresAt <= reservedAt) throw new ArgumentException("Seat reservation expiry must be after reservation time.", nameof(expiresAt));
		if (seatNumber <= 0) throw new ArgumentOutOfRangeException(nameof(seatNumber));
		TenantId = tenantId;
		OnboardingCaseId = Check.NotDefaultOrNull<Guid>(onboardingCaseId, nameof(onboardingCaseId));
		AcademicYearId = Check.NotDefaultOrNull<Guid>(academicYearId, nameof(academicYearId));
		StandardId = Check.NotDefaultOrNull<Guid>(standardId, nameof(standardId));
		DivisionId = Check.NotDefaultOrNull<Guid>(divisionId, nameof(divisionId));
		SeatNumber = seatNumber;
		ReservedAt = reservedAt.ToUniversalTime();
		ExpiresAt = expiresAt.ToUniversalTime();
		Status = StudentSeatReservationStatus.Reserved;
	}

	public bool IsLive(DateTime at) => Status == StudentSeatReservationStatus.Reserved && ExpiresAt > at.ToUniversalTime();

	public void Commit(DateTime at)
	{
		if (!IsLive(at)) throw new BusinessException(StudentOnboardingErrorCodes.SeatReservationExpired);
		Status = StudentSeatReservationStatus.Committed;
		CommittedAt = at.ToUniversalTime();
	}

	public void Release(string reason, DateTime at)
	{
		if (Status != StudentSeatReservationStatus.Reserved) return;
		Status = StudentSeatReservationStatus.Released;
		ReleasedAt = at.ToUniversalTime();
		ReleaseReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), 500).Trim();
	}

	public void Expire(DateTime at)
	{
		if (Status == StudentSeatReservationStatus.Reserved && ExpiresAt <= at.ToUniversalTime())
			Status = StudentSeatReservationStatus.Expired;
	}
}
