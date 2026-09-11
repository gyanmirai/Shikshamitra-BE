using System;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students;

public class StudentOnboarding_Tests
{
	[Fact]
	public void Case_tracks_SLA_placement_identifiers_and_terminal_activation()
	{
		var now = DateTime.UtcNow;
		var onboardingCase = CreateCase(now);
		var divisionId = Guid.NewGuid();
		var reservationId = Guid.NewGuid();
		var enrollmentId = Guid.NewGuid();

		onboardingCase.DueAt.ShouldBe(now.AddHours(48), TimeSpan.FromMilliseconds(1));
		onboardingCase.RecordPlacement(divisionId, reservationId, now.AddMinutes(1));
		onboardingCase.RecordIdentifier(Guid.NewGuid(), 17, now.AddMinutes(2));
		onboardingCase.MarkReady(now.AddMinutes(3));
		onboardingCase.Complete(enrollmentId, now.AddMinutes(4));

		onboardingCase.Status.ShouldBe(StudentOnboardingStatus.Completed);
		onboardingCase.StudentEnrollmentId.ShouldBe(enrollmentId);
		onboardingCase.DivisionId.ShouldBe(divisionId);
		onboardingCase.RollNumber.ShouldBe(17);
		Should.Throw<BusinessException>(() => onboardingCase.AssignOwner(Guid.NewGuid()));
	}

	[Fact]
	public void Cancellation_is_terminal_and_keeps_identity_reference()
	{
		var onboardingCase = CreateCase(DateTime.UtcNow);
		var studentId = onboardingCase.StudentId;

		onboardingCase.Cancel("Family deferred admission", DateTime.UtcNow.AddMinutes(1));

		onboardingCase.Status.ShouldBe(StudentOnboardingStatus.Cancelled);
		onboardingCase.StudentId.ShouldBe(studentId);
		onboardingCase.CancellationReason.ShouldBe("Family deferred admission");
		Should.Throw<BusinessException>(() => onboardingCase.RecordProgress(DateTime.UtcNow));
	}

	[Fact]
	public void Activation_failure_requires_attention_and_can_resume_after_staff_progress()
	{
		var onboardingCase = CreateCase(DateTime.UtcNow);

		onboardingCase.RequireAttention("Fee clearance could not be verified.");

		onboardingCase.Status.ShouldBe(StudentOnboardingStatus.NeedsAttention);
		onboardingCase.LastError.ShouldBe("Fee clearance could not be verified.");

		onboardingCase.RecordProgress(DateTime.UtcNow.AddMinutes(1));
		onboardingCase.Status.ShouldBe(StudentOnboardingStatus.InProgress);
		onboardingCase.LastError.ShouldBeNull();
	}

	[Fact]
	public void Historical_import_is_completed_without_fabricating_guardian_verification()
	{
		var completedAt = DateTime.UtcNow.AddYears(-1);
		var onboardingCase = CreateHistoricalCase(completedAt);
		var enrollmentId = Guid.NewGuid();
		var divisionId = Guid.NewGuid();

		onboardingCase.CompleteHistoricalImport(enrollmentId, divisionId, 23, completedAt);

		onboardingCase.Status.ShouldBe(StudentOnboardingStatus.Completed);
		onboardingCase.IsHistoricalBackfill.ShouldBeTrue();
		onboardingCase.GuardianContactVerifiedAt.ShouldBeNull();
		onboardingCase.StudentEnrollmentId.ShouldBe(enrollmentId);
		onboardingCase.DivisionId.ShouldBe(divisionId);
		onboardingCase.RollNumber.ShouldBe(23);
		onboardingCase.DueAt.ShouldBe(completedAt, TimeSpan.FromMilliseconds(1));
	}

	[Fact]
	public void Enrollment_historical_link_is_idempotent_and_rejects_a_conflicting_case()
	{
		var onboardingCaseId = Guid.NewGuid();
		var enrollment = StudentEnrollment.Create(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(),
			Guid.NewGuid(), Guid.NewGuid());

		enrollment.LinkOnboardingCase(onboardingCaseId);
		enrollment.LinkOnboardingCase(onboardingCaseId);

		enrollment.OnboardingCaseId.ShouldBe(onboardingCaseId);
		Should.Throw<BusinessException>(() => enrollment.LinkOnboardingCase(Guid.NewGuid()))
			.Code.ShouldBe(StudentOnboardingErrorCodes.ConcurrencyConflict);
	}

	[Fact]
	public void Seat_reservation_cannot_commit_after_expiry()
	{
		var now = DateTime.UtcNow;
		var reservation = new StudentSeatReservation(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(),
			Guid.NewGuid(), Guid.NewGuid(), 1, now, now.AddMinutes(30));

		reservation.IsLive(now.AddMinutes(29)).ShouldBeTrue();
		Should.Throw<BusinessException>(() => reservation.Commit(now.AddMinutes(31)))
			.Code.ShouldBe(StudentOnboardingErrorCodes.SeatReservationExpired);
		reservation.Expire(now.AddMinutes(31));
		reservation.Status.ShouldBe(StudentSeatReservationStatus.Expired);
	}

	[Fact]
	public void Identifier_series_formats_values_and_honours_the_maximum()
	{
		var now = DateTime.UtcNow;
		var series = new StudentIdentifierSeries(Guid.NewGuid(), null, Guid.NewGuid(), null,
			StudentIdentifierType.GrNumber, "GR-2027", "General Register 2027-28", "GR/27/", "", 5,
			9, 10, now.AddDays(-1), now.AddDays(1));

		series.Allocate(now).ShouldBe((9L, "GR/27/00009"));
		series.Allocate(now).ShouldBe((10L, "GR/27/00010"));
		Should.Throw<BusinessException>(() => series.Allocate(now))
			.Code.ShouldBe(StudentOnboardingErrorCodes.IdentifierSeriesUnavailable);
	}

	[Fact]
	public void Identifier_series_rejects_manual_use_outside_its_effective_window()
	{
		var now = DateTime.UtcNow;
		var series = new StudentIdentifierSeries(Guid.NewGuid(), null, Guid.NewGuid(), null,
			StudentIdentifierType.GrNumber, "GR-FUTURE", "Future series", "GR/", "", 5,
			1, null, now.AddDays(1), now.AddDays(2));

		Should.Throw<BusinessException>(() => series.EnsureAvailable(now))
			.Code.ShouldBe(StudentOnboardingErrorCodes.IdentifierSeriesUnavailable);
	}

	[Fact]
	public void Division_change_clears_roll_but_keeps_reserved_permanent_identifier()
	{
		var now = DateTime.UtcNow;
		var onboardingCase = CreateCase(now);
		var allocationId = Guid.NewGuid();
		onboardingCase.RecordPlacement(Guid.NewGuid(), Guid.NewGuid(), now.AddMinutes(1));
		onboardingCase.RecordIdentifier(allocationId, 17, now.AddMinutes(2));

		onboardingCase.ClearRollNumberForPlacementChange(now.AddMinutes(3));

		onboardingCase.RollNumber.ShouldBeNull();
		onboardingCase.IdentifierAllocationId.ShouldBe(allocationId);
		onboardingCase.Status.ShouldBe(StudentOnboardingStatus.InProgress);
	}

	[Fact]
	public void Waived_step_records_actor_reason_and_completion()
	{
		var actorId = Guid.NewGuid();
		var step = new StudentOnboardingStep(Guid.NewGuid(), null, Guid.NewGuid(),
			StudentOnboardingStepKeys.MedicalAndPreviousSchool, "Medical", 5, true);

		step.Waive(actorId, DateTime.UtcNow, "Not required for this age group");

		step.Status.ShouldBe(StudentOnboardingStepStatus.Waived);
		step.CompletedByUserId.ShouldBe(actorId);
		step.WaiverReason.ShouldBe("Not required for this age group");
		step.CompletedAt.ShouldNotBeNull();
	}

	private static StudentOnboardingCase CreateCase(DateTime now) => new(
		Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
		Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
		$"finalize:{Guid.NewGuid():N}", now, now.AddHours(48));

	private static StudentOnboardingCase CreateHistoricalCase(DateTime completedAt) => new(
		Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
		Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
		$"historical:{Guid.NewGuid():N}", null, completedAt.AddHours(1));
}
