using ShikshaMitra.HR.WorkShifts;
using System;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.TenantSettings;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class PayrollLateArrivalOffset_Tests
{
	[Theory]
	[InlineData(0, 30, false)]
	[InlineData(330, 30, false)]
	[InlineData(-300, 30, false)]
	[InlineData(0, 31, true)]
	[InlineData(330, 31, true)]
	[InlineData(-300, 31, true)]
	public void Same_stored_instant_has_same_grace_boundary_regardless_of_reader_offset(
		int offsetMinutes, int minute, bool expectedLate)
	{
		var shift = new EmployeeShiftAssignment(Guid.NewGuid(), Guid.NewGuid(), new EmployeeWorkShift(Guid.NewGuid(), Guid.NewGuid(), "Office", new(9, 0), new(17, 0)), new DateOnly(2026, 1, 1));
		var timeIn = new DateTimeOffset(2026, 6, 12, 9, minute, 0, TimeSpan.Zero)
			.ToOffset(TimeSpan.FromMinutes(offsetMinutes));
		PayrollLateArrivalDeductionCalculator.IsLateArrival(timeIn, shift, 30).ShouldBe(expectedLate);
	}
}