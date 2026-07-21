using System;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionCrmEntranceExamRecord_Tests
{
	[Fact]
	public void Absence_requires_an_explicit_eligibility_decision()
	{
		var record = CreateRecord(CreateSlot("First attempt"));
		var markedAt = DateTime.UtcNow;

		record.MarkAttendance(AdmissionCrmExamAttendanceStatus.Absent, markedAt);

		record.ExamStatus.ShouldBe(EntranceExamStatusEnum.Absent);
		record.EligibilityDecision.ShouldBe(AdmissionCrmEligibilityDecision.Pending);
		record.EligibilityDecisionReason.ShouldBeNull();
		record.EligibilityDecidedAt.ShouldBeNull();
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
		Guid? academicYearId = null)
	{
		var start = DateTimeOffset.UtcNow.AddDays(2);
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
			start,
			start.AddHours(2),
			20,
			101);
	}
}
