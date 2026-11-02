using System;
using ShikshaMitra.HR.WorkShifts;
using Shouldly;
using Volo.Abp;
using Xunit;
namespace ShikshaMitra.HR;
public class EmployeeWorkShift_Tests
{
 [Fact] public void School_date_preserves_clock_kind_and_crosses_midnight_once()
 {
  var utc = new DateTime(2026,11,2,15,30,0,DateTimeKind.Utc);
  EmployeeShiftResolver.ToSchoolDate(utc,"Asia/Kolkata").ShouldBe(new DateOnly(2026,11,2));
  EmployeeShiftResolver.ToSchoolDate(utc.ToLocalTime(),"Asia/Kolkata").ShouldBe(new DateOnly(2026,11,2));
  EmployeeShiftResolver.ToSchoolDate(utc.AddHours(3),"Asia/Kolkata").ShouldBe(new DateOnly(2026,11,3));
 }

	[Theory]
	[InlineData(8, 8)]
	[InlineData(22, 6)]
	public void Daytime_shift_must_have_positive_duration(int start, int end) => Should.Throw<BusinessException>(() => new EmployeeWorkShift(Guid.NewGuid(), Guid.NewGuid(), "Shift", new(start, 0), new(end, 0)));
	[Fact]
	public void Assignment_preserves_snapshot_when_catalogue_changes()
	{
		var shift = new EmployeeWorkShift(Guid.NewGuid(), Guid.NewGuid(), "Office", new(9, 0), new(17, 0));
		var assignment = new EmployeeShiftAssignment(Guid.NewGuid(), Guid.NewGuid(), shift, new(2026, 11, 2), new(2026, 12, 1));
		shift.Update("Renamed", new(10, 0), new(18, 0), false);
		assignment.ShiftName.ShouldBe("Office"); assignment.StartTime.ShouldBe(new TimeOnly(9, 0));
		assignment.Covers(new(2026, 11, 2)).ShouldBeTrue(); assignment.Covers(new(2026, 11, 30)).ShouldBeTrue(); assignment.Covers(new(2026, 12, 1)).ShouldBeFalse();
	}
	[Fact]
	public void Schedule_resolves_each_date_and_rejects_gaps()
	{
		var tenant = Guid.NewGuid(); var employee = Guid.NewGuid();
		var a = new EmployeeShiftAssignment(Guid.NewGuid(), employee, new(Guid.NewGuid(), tenant, "Morning", new(7, 0), new(13, 0)), new(2026, 11, 1), new(2026, 11, 15));
		var b = new EmployeeShiftAssignment(Guid.NewGuid(), employee, new(Guid.NewGuid(), tenant, "Office", new(9, 0), new(17, 0)), new(2026, 11, 15));
		var schedule = new EmployeeWorkSchedule([a, b]);
		schedule.Require(new(2026, 11, 14)).StartTime.ShouldBe(new TimeOnly(7, 0));
		schedule.Require(new(2026, 11, 15)).StartTime.ShouldBe(new TimeOnly(9, 0));
		Should.Throw<BusinessException>(() => schedule.Require(new(2026, 10, 31)));
	}
	[Fact]
	public void Inactive_shifts_cannot_receive_new_assignments()
	{
		var shift = new EmployeeWorkShift(Guid.NewGuid(), Guid.NewGuid(), "Office", new(9, 0), new(17, 0));
		shift.Update("Office", new(9, 0), new(17, 0), false);
		Should.Throw<BusinessException>(() => new EmployeeShiftAssignment(Guid.NewGuid(), Guid.NewGuid(), shift, new(2026, 11, 2)));
	}
}