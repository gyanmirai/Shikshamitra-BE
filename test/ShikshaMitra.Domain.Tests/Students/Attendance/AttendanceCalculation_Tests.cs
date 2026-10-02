using System;
using System.Linq;
using ShikshaMitra.Students.Attendance;
using ShikshaMitra.Students.StudentEnrollments;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students;

public class AttendanceCalculation_Tests
{
	private static readonly DateTime Start = new(2026, 6, 1);
	[Fact]
	public void Missing_dates_remain_provisional_without_becoming_absent()
	{
		var rows = Enumerable.Range(0, 20).Select(i => new AttendanceCalculationDay(Start.AddDays(i), true, i < 18,
			i < 17 ? 1 : 0, Status: i < 17 ? AttendanceStatus.Present : i == 17 ? AttendanceStatus.Absent : AttendanceStatus.Pending));
		var result = AttendanceCalculation.Calculate(rows);
		result.ExpectedDays.ShouldBe(20); result.RecordedDays.ShouldBe(18); result.MissingDays.ShouldBe(2);
		result.Percentage!.Value.ShouldBe(94.44); result.IsProvisional.ShouldBeTrue();
	}
	[Fact]
	public void No_expected_days_has_no_percentage()
	{
		var result = AttendanceCalculation.Calculate([new(Start, false, false, 0)]);
		result.Percentage.ShouldBeNull(); result.ExpectedDays.ShouldBe(0); result.IsProvisional.ShouldBeFalse();
	}
	[Fact]
	public void Recorded_absence_is_a_real_zero_percentage()
	{
		var result = AttendanceCalculation.Calculate([new(Start, true, true, 0, Status: AttendanceStatus.Absent)]);
		result.Percentage.ShouldBe(0); result.RecordedDays.ShouldBe(1); result.IsProvisional.ShouldBeFalse();
	}
	[Fact]
	public void Half_day_credits_and_half_leave_denominator_are_distinct()
	{
		var result = AttendanceCalculation.Calculate([new(Start, true, true, .5, Status: AttendanceStatus.Present),
			new(Start.AddDays(1), true, true, .5, .5, Status: AttendanceStatus.Present)]);
		result.RecordedDays.ShouldBe(2); result.PresentCredit.ShouldBe(1); result.DenominatorDays.ShouldBe(1.5);
		result.Percentage.ShouldBe(66.67);
	}
	[Fact]
	public void Same_date_division_change_preserves_one_recorded_outcome()
	{
		var result = AttendanceCalculation.Calculate([new(Start, true, false, 0, IsProvisional: true),
			new(Start, true, true, 1, Status: AttendanceStatus.Present)]);
		result.ExpectedDays.ShouldBe(1); result.RecordedDays.ShouldBe(1); result.Percentage.ShouldBe(100);
		result.IsProvisional.ShouldBeFalse();
	}
	[Fact]
	public void Conflicting_recorded_dates_remain_unknown_for_review()
	{
		var result = AttendanceCalculation.Calculate([new(Start, true, true, 1, Status: AttendanceStatus.Present),
			new(Start, true, true, 0, Status: AttendanceStatus.Absent)]);
		result.ExpectedDays.ShouldBe(1); result.RecordedDays.ShouldBe(0); result.MissingDays.ShouldBe(1);
		result.Percentage.ShouldBeNull(); result.IsProvisional.ShouldBeTrue();
	}
	[Fact]
	public void A_complete_but_unsubmitted_draft_is_still_provisional()
	{
		AttendanceCalculation.Calculate([new(Start, true, true, 1, IsProvisional: true, Status: AttendanceStatus.Present)]).IsProvisional.ShouldBeTrue();
	}
	[Fact]
	public void Enrollment_requires_confirmed_start_and_retains_dated_placement_and_exit()
	{
		var oldClass = Guid.NewGuid(); var oldDivision = Guid.NewGuid(); var newClass = Guid.NewGuid(); var newDivision = Guid.NewGuid();
		var enrollment = StudentEnrollment.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), oldClass, oldDivision, 7);
		enrollment.AttendancePlacementOn(Start).ShouldBeNull();
		enrollment.ConfirmAttendanceStart(Start); enrollment.AttendancePlacementOn(Start.AddDays(-1)).ShouldBeNull();
		enrollment.ChangePlacement(newClass, newDivision, 4, Start.AddDays(10));
		enrollment.AttendancePlacementOn(Start.AddDays(9))!.DivisionId.ShouldBe(oldDivision);
		enrollment.AttendancePlacementOn(Start.AddDays(9))!.RollNumber.ShouldBe(7);
		enrollment.AttendancePlacementOn(Start.AddDays(10))!.DivisionId.ShouldBe(newDivision);
		enrollment.Deactivate(Start.AddDays(20)); enrollment.AttendancePlacementOn(Start.AddDays(19))!.IsActive.ShouldBeTrue();
		enrollment.AttendancePlacementOn(Start.AddDays(20))!.IsActive.ShouldBeFalse();
		enrollment.Activate(Start.AddDays(25)); enrollment.AttendancePlacementOn(Start.AddDays(24))!.IsActive.ShouldBeFalse();
		enrollment.AttendancePlacementOn(Start.AddDays(25))!.IsActive.ShouldBeTrue();
	}
	[Fact]
	public void Report_range_uses_the_placement_at_start_and_only_moves_inside_the_range()
	{
		var oldDivision = Guid.NewGuid(); var newDivision = Guid.NewGuid();
		var enrollment = StudentEnrollment.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), oldDivision);
		enrollment.ConfirmAttendanceStart(Start);
		enrollment.ChangePlacement(Guid.NewGuid(), newDivision, 4, Start.AddDays(10));
		enrollment.AttendancePlacementsBetween(Start.AddDays(-5), Start.AddDays(-1)).ShouldBeEmpty();
		enrollment.AttendancePlacementsBetween(Start.AddDays(9), Start.AddDays(10)).Select(p => p.DivisionId)
			.ShouldBe(new Guid?[] { oldDivision, newDivision });
		enrollment.AttendancePlacementsBetween(Start.AddDays(10), Start.AddDays(20)).Select(p => p.DivisionId)
			.ShouldBe(new Guid?[] { newDivision });
	}
	[Fact]
	public void Confirmed_start_cannot_be_silently_rewritten()
	{
		var enrollment = StudentEnrollment.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
		enrollment.ConfirmAttendanceStart(Start); enrollment.ConfirmAttendanceStart(Start);
		Should.Throw<BusinessException>(() => enrollment.ConfirmAttendanceStart(Start.AddDays(-1)));
	}
}
