using System;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionSeatAllocation_Tests
{
	[Fact]
	public void Pool_prevents_overbooking_without_an_explicit_override()
	{
		var pool = Pool(1);
		pool.Reserve(false);
		var error = Should.Throw<BusinessException>(() => pool.Reserve(false));
		error.Code.ShouldBe(AdmissionSeatStageErrorCodes.CapacityFull);
		pool.Reserve(true);
		pool.IsOverCapacity.ShouldBeTrue();
	}

	[Fact]
	public void Confirm_and_release_keep_authoritative_counts_consistent()
	{
		var pool = Pool(2);
		pool.Reserve(false);
		pool.Confirm();
		pool.ReservedCount.ShouldBe(0);
		pool.ConfirmedCount.ShouldBe(1);
		pool.Release(AdmissionSeatReservationStatus.Confirmed);
		pool.RemainingCapacity.ShouldBe(2);
	}

	[Fact]
	public void Reservation_enforces_expiry_and_renewal_limits()
	{
		var now = DateTime.UtcNow;
		var reservation = Reservation(now);
		reservation.Renew(now, now.AddHours(96), 1, Guid.NewGuid(), "Family requested time.");
		reservation.RenewalCount.ShouldBe(1);
		Should.Throw<BusinessException>(() => reservation.Renew(now, now.AddHours(120), 1, null, null))
			.Code.ShouldBe(AdmissionSeatStageErrorCodes.RenewalNotAllowed);
	}

	[Fact]
	public void Expired_reservation_cannot_be_confirmed()
	{
		var now = DateTime.UtcNow;
		var reservation = Reservation(now);
		Should.Throw<BusinessException>(() => reservation.Confirm(now.AddHours(73), null, null))
			.Code.ShouldBe(AdmissionSeatStageErrorCodes.InvalidState);
		reservation.Expire(now.AddHours(73));
		reservation.Status.ShouldBe(AdmissionSeatReservationStatus.Expired);
	}

	private static AdmissionSeatPool Pool(int capacity) => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), capacity);
	private static AdmissionSeatReservation Reservation(DateTime now) => new(Guid.NewGuid(), Guid.NewGuid(),
		Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), AdmissionSeatReservationStatus.Reserved,
		now, now.AddHours(72), Guid.NewGuid(), null);
}
