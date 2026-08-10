using System;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionCrmEvent_Tests
{
	[Fact]
	public void Published_event_enforces_capacity_and_prevents_duplicate_registration()
	{
		var now = DateTime.UtcNow;
		var admissionEvent = CreateEvent(now, capacity: 1);
		admissionEvent.Publish(now);
		var leadId = Guid.NewGuid();
		admissionEvent.Reserve(Guid.NewGuid(), leadId, now.AddMinutes(1), null);

		Should.Throw<BusinessException>(() =>
			admissionEvent.Reserve(Guid.NewGuid(), leadId, now.AddMinutes(2), null))
			.Code.ShouldBe("AdmissionCrm:EventApplicantAlreadyRegistered");
		Should.Throw<BusinessException>(() =>
			admissionEvent.Reserve(Guid.NewGuid(), Guid.NewGuid(), now.AddMinutes(2), null))
			.Code.ShouldBe("AdmissionCrm:EventCapacityReached");
	}

	[Fact]
	public void Cancellation_releases_capacity_and_event_cancellation_cancels_reservations()
	{
		var now = DateTime.UtcNow;
		var admissionEvent = CreateEvent(now, capacity: 1);
		admissionEvent.Publish(now);
		var registration = admissionEvent.Reserve(Guid.NewGuid(), Guid.NewGuid(), now, null);
		admissionEvent.CancelRegistration(registration.Id, now.AddMinutes(1), "Family unavailable");
		admissionEvent.ReservedCount.ShouldBe(0);

		var replacement = admissionEvent.Reserve(Guid.NewGuid(), Guid.NewGuid(), now.AddMinutes(2), null);
		admissionEvent.Cancel(now.AddMinutes(3), "School closed");

		admissionEvent.Status.ShouldBe(AdmissionCrmEventStatus.Cancelled);
		replacement.Status.ShouldBe(AdmissionCrmEventRegistrationStatus.Cancelled);
	}

	[Fact]
	public void Check_in_and_no_show_are_distinct_terminal_attendance_outcomes()
	{
		var now = DateTime.UtcNow;
		var admissionEvent = CreateEvent(now, capacity: 2);
		admissionEvent.Publish(now);
		var present = admissionEvent.Reserve(Guid.NewGuid(), Guid.NewGuid(), now, null);
		var absent = admissionEvent.Reserve(Guid.NewGuid(), Guid.NewGuid(), now, null);

		admissionEvent.CheckIn(present.Id, now.AddHours(2));
		admissionEvent.MarkNoShow(absent.Id, now.AddHours(3));

		present.Status.ShouldBe(AdmissionCrmEventRegistrationStatus.CheckedIn);
		absent.Status.ShouldBe(AdmissionCrmEventRegistrationStatus.NoShow);
	}

	[Fact]
	public void Invalid_schedule_and_capacity_reduction_below_reservations_are_rejected()
	{
		var now = DateTime.UtcNow;
		Should.Throw<BusinessException>(() => new AdmissionCrmEvent(
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Open house",
			AdmissionCrmEventType.OpenHouse, now.AddHours(2), now.AddHours(1),
			"Main campus", 10, null, null));

		var admissionEvent = CreateEvent(now, capacity: 2);
		admissionEvent.Publish(now);
		admissionEvent.Reserve(Guid.NewGuid(), Guid.NewGuid(), now, null);
		Should.Throw<BusinessException>(() => admissionEvent.UpdateDetails(
			"Open house", AdmissionCrmEventType.OpenHouse, now.AddHours(1), now.AddHours(2),
			"Main campus", 0, null, null));
	}

	[Fact]
	public void Attendance_cannot_be_recorded_before_the_operational_window_or_after_cancellation()
	{
		var now = DateTime.UtcNow;
		var admissionEvent = CreateEvent(now, capacity: 2);
		admissionEvent.Publish(now);
		var early = admissionEvent.Reserve(Guid.NewGuid(), Guid.NewGuid(), now, null);
		Should.Throw<BusinessException>(() => admissionEvent.CheckIn(early.Id, now.AddHours(-4)))
			.Code.ShouldBe("AdmissionCrm:EventCheckInTooEarly");

		var cancelled = admissionEvent.Reserve(Guid.NewGuid(), Guid.NewGuid(), now, null);
		admissionEvent.Cancel(now.AddMinutes(1), "Weather warning");
		Should.Throw<BusinessException>(() => admissionEvent.CheckIn(cancelled.Id, now.AddHours(1)))
			.Code.ShouldBe("AdmissionCrm:EventNotOpenForAttendance");
	}

	private static AdmissionCrmEvent CreateEvent(DateTime now, int capacity) =>
		new(
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid(),
			"Admissions open house",
			AdmissionCrmEventType.OpenHouse,
			now.AddHours(1),
			now.AddHours(2),
			"Main campus auditorium",
			capacity,
			Guid.NewGuid(),
			"Please arrive 15 minutes early.");
}
