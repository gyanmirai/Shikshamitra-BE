using System;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmFinalAdmission_Tests
{
	[Fact]
	public void Seat_Pool_Should_Protect_Capacity_And_Require_A_Reasoned_Override()
	{
		var pool = CreatePool(capacity: 1);

		pool.ReserveSeat(false, null).ShouldBeFalse();
		pool.ReservedSeats.ShouldBe(1);
		Should.Throw<BusinessException>(() => pool.ReserveSeat(false, null))
			.Code.ShouldBe("AdmissionCrm:SeatPoolCapacityExhausted");
		Should.Throw<BusinessException>(() => pool.ReserveSeat(true, null))
			.Code.ShouldBe("AdmissionCrm:SeatCapacityOverrideReasonRequired");

		pool.ReserveSeat(true, "Principal approved one additional seat.").ShouldBeTrue();
		pool.IsOverCapacity.ShouldBeTrue();
		pool.OccupiedSeats.ShouldBe(2);
	}

	[Fact]
	public void Seat_Pool_Should_Keep_Reservation_Acceptance_And_Cancellation_Counters_Consistent()
	{
		var pool = CreatePool(capacity: 1);
		pool.ReserveSeat(false, null);
		pool.AcceptReservedSeat();

		pool.ReservedSeats.ShouldBe(0);
		pool.AcceptedSeats.ShouldBe(1);

		pool.ReleaseSeat(wasAccepted: true, cancelled: true);
		pool.AcceptedSeats.ShouldBe(0);
		pool.CancelledSeats.ShouldBe(1);
		pool.RemainingCapacity.ShouldBe(1);
	}

	[Fact]
	public void Offer_Should_Require_Distinct_Approval_And_Respect_Issue_Window()
	{
		var now = DateTime.UtcNow;
		var submitter = Guid.NewGuid();
		var approver = Guid.NewGuid();
		var offer = CreateOffer(now.AddHours(1), now.AddDays(2));

		offer.SubmitForApproval(submitter, now);
		Should.Throw<BusinessException>(() => offer.Approve(submitter, now))
			.Code.ShouldBe("AdmissionCrm:DistinctOfferApproverRequired");
		offer.Approve(approver, now);
		Should.Throw<BusinessException>(() => offer.Issue(Guid.NewGuid(), now))
			.Code.ShouldBe("AdmissionCrm:SeatOfferNotYetValid");

		offer.Issue(Guid.NewGuid(), now.AddHours(1));
		offer.Accept("Applicant OTP link", now.AddHours(2));
		offer.CancelAccepted("Admission withdrawn before activation.", now.AddHours(3));

		offer.Status.ShouldBe(AdmissionCrmSeatOfferStatus.Cancelled);
		offer.ResponseReason.ShouldBe("Admission withdrawn before activation.");
	}

	[Fact]
	public void Reservation_Should_Not_Commit_After_Expiry()
	{
		var now = DateTime.UtcNow;
		var reservation = new AdmissionCrmSeatReservation(
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
			now, now.AddHours(1), false, null);

		Should.Throw<BusinessException>(() => reservation.Commit(now.AddHours(2)))
			.Code.ShouldBe("AdmissionCrm:SeatReservationExpired");
	}

	[Fact]
	public void Admission_Decision_Should_Require_Override_Reason()
	{
		var decision = new AdmissionCrmAdmissionDecision(
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

		Should.Throw<BusinessException>(() => decision.Decide(
			AdmissionCrmAdmissionDecisionStatus.Approved,
			Guid.NewGuid(), Guid.NewGuid(), "Approved", true, null, Guid.NewGuid(), DateTime.UtcNow))
			.Code.ShouldBe("AdmissionCrm:AdmissionDecisionOverrideReasonRequired");
	}

	private static AdmissionCrmSeatPool CreatePool(int capacity) => new(
		Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, "GRADE-1-GEN", "Grade 1 general",
		null, "Main", "Grade 1", "CBSE", "English", "General", capacity, capacity);

	private static AdmissionCrmSeatOffer CreateOffer(DateTime validFrom, DateTime validUntil) => new(
		Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, null, null,
		"OFF-TEST-0001", 1, AdmissionCrmSeatOfferType.Final, "{}", "en", validFrom, validUntil);
}
