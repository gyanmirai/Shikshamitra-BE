using System;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionCrmEntranceExamRecord_Tests
{
	[Fact]
	public void Inactive_exam_slot_cannot_accept_a_reservation()
	{
		var slot = CreateSlot("Inactive slot");
		slot.SetActive(false);

		var exception = Should.Throw<BusinessException>(() =>
			slot.EnsureCanAcceptReservation(DateTimeOffset.UtcNow));

		exception.Code.ShouldBe("AdmissionCrm:ExamSlotInactive");
	}

	[Fact]
	public void Started_exam_slot_cannot_accept_a_reservation()
	{
		var now = DateTimeOffset.UtcNow;
		var slot = CreateSlot("Started slot", start: now.AddMinutes(-30));

		var exception = Should.Throw<BusinessException>(() => slot.EnsureCanAcceptReservation(now));

		exception.Code.ShouldBe("AdmissionCrm:ExamSlotAlreadyStarted");
	}

	[Fact]
	public void New_exam_slot_has_a_capacity_concurrency_stamp()
	{
		var slot = CreateSlot("Last seat");

		slot.ConcurrencyStamp.ShouldNotBeNullOrWhiteSpace();
	}

	[Theory]
	[InlineData(AdmissionCrmExamAttendanceStatus.Absent)]
	[InlineData(AdmissionCrmExamAttendanceStatus.NoShow)]
	[InlineData(AdmissionCrmExamAttendanceStatus.Excused)]
	public void Non_attendance_requires_an_explicit_eligibility_decision(
		AdmissionCrmExamAttendanceStatus attendanceStatus)
	{
		var record = CreateRecord(CreateSlot("First attempt"));
		var markedAt = DateTime.UtcNow;

		record.MarkAttendance(attendanceStatus, markedAt);

		record.AttendanceStatus.ShouldBe(attendanceStatus);
		record.ExamStatus.ShouldBe(EntranceExamStatusEnum.Absent);
		record.EligibilityDecision.ShouldBe(AdmissionCrmEligibilityDecision.Pending);
		record.EligibilityDecisionReason.ShouldBeNull();
		record.EligibilityDecidedAt.ShouldBeNull();
	}

	[Theory]
	[InlineData(AdmissionCrmExamAttendanceStatus.Absent)]
	[InlineData(AdmissionCrmExamAttendanceStatus.NoShow)]
	[InlineData(AdmissionCrmExamAttendanceStatus.Excused)]
	public void Result_cannot_be_recorded_for_non_attendance(
		AdmissionCrmExamAttendanceStatus attendanceStatus)
	{
		var record = CreateRecord(CreateSlot("Missed attempt"));
		record.MarkAttendance(attendanceStatus, DateTime.UtcNow);

		var exception = Should.Throw<BusinessException>(() =>
			record.RecordResult(100, 40, 85, null, DateTime.UtcNow));

		exception.Code.ShouldBe("AdmissionCrm:CannotRecordResultForAbsentApplicant");
	}

	[Theory]
	[InlineData(AdmissionCrmExamAttendanceStatus.NotMarked)]
	[InlineData((AdmissionCrmExamAttendanceStatus)999)]
	public void Invalid_attendance_state_is_rejected(AdmissionCrmExamAttendanceStatus attendanceStatus)
	{
		var record = CreateRecord(CreateSlot("Invalid attendance"));

		var exception = Should.Throw<BusinessException>(() =>
			record.MarkAttendance(attendanceStatus, DateTime.UtcNow));

		exception.Code.ShouldBe("AdmissionCrm:InvalidAttendanceStatus");
	}

	[Fact]
	public void Reschedule_clears_the_previous_decision_metadata()
	{
		var record = CreateRecord(CreateSlot("First attempt"));
		record.MarkAttendance(AdmissionCrmExamAttendanceStatus.Absent, DateTime.UtcNow);
		record.SetEligibilityDecision(
			AdmissionCrmEligibilityDecision.Rejected,
			"Family did not request another attempt.",
			false,
			null,
			null,
			DateTime.UtcNow);

		record.Reschedule(
			CreateSlot("Second attempt", record.TenantId, record.AcademicYearId),
			false,
			null,
			DateTime.UtcNow);

		record.AttendanceStatus.ShouldBe(AdmissionCrmExamAttendanceStatus.NotMarked);
		record.ExamStatus.ShouldBe(EntranceExamStatusEnum.Pending);
		record.EligibilityDecision.ShouldBe(AdmissionCrmEligibilityDecision.Pending);
		record.EligibilityDecisionReason.ShouldBeNull();
		record.EligibilityOverrideUsed.ShouldBeFalse();
		record.EligibilityOverrideReason.ShouldBeNull();
		record.EligibilityDecidedAt.ShouldBeNull();
	}

	private static AdmissionCrmEntranceExamRecord CreateRecord(AdmissionCrmExamSlot slot) =>
		new(Guid.NewGuid(), slot.TenantId, slot.AcademicYearId, Guid.NewGuid(), slot, false, null, DateTime.UtcNow);

	private static AdmissionCrmExamSlot CreateSlot(
		string name,
		Guid? tenantId = null,
		Guid? academicYearId = null,
		DateTimeOffset? start = null)
	{
		var startDate = start ?? DateTimeOffset.UtcNow.AddDays(2);
		return new AdmissionCrmExamSlot(
			Guid.NewGuid(),
			tenantId ?? Guid.NewGuid(),
			academicYearId ?? Guid.NewGuid(),
			null,
			null,
			null,
			name,
			null,
			null,
			startDate,
			startDate.AddHours(2),
			20,
			101);
	}
}
