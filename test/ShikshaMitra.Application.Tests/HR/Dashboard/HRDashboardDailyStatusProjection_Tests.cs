using System;
using System.Linq;
using ShikshaMitra.HR.AttendanceAndLeaves;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Dashboard;

public class HRDashboardDailyStatusProjection_Tests
{
	[Theory]
	[InlineData(null, false, false, true, false, "NotMarked")]
	[InlineData(null, false, true, true, false, "NotMarked")]
	[InlineData(null, true, false, true, false, "OnLeave")]
	[InlineData(EmployeeAttendanceStatusType.FullDayLeave, true, false, true, false, "OnLeave")]
	[InlineData(EmployeeAttendanceStatusType.FullDayLeave, false, false, true, false, "Absent")]
	[InlineData(EmployeeAttendanceStatusType.FullDayLeave, false, false, true, true, "NotMarked")]
	[InlineData(EmployeeAttendanceStatusType.HalfDayLeave, false, false, true, true, "NotMarked")]
	[InlineData(EmployeeAttendanceStatusType.Present, true, false, true, false, "Present")]
	[InlineData(EmployeeAttendanceStatusType.HalfDayLeave, true, false, true, false, "HalfDay")]
	[InlineData(null, false, false, false, false, "NotScheduled")]
	[InlineData(EmployeeAttendanceStatusType.FullDayLeave, false, false, false, false, "NotScheduled")]
	[InlineData(EmployeeAttendanceStatusType.Present, false, false, false, false, "Present")]
	[InlineData(null, false, false, null, false, "NeedsSetup")]
	[InlineData(EmployeeAttendanceStatusType.Present, false, false, null, false, "Present")]
	public void Should_Classify_Daily_Staff_Without_Inventing_Absence(
		EmployeeAttendanceStatusType? recorded, bool fullLeave, bool halfLeave, bool? working, bool incomplete, string expected)
	{
		HRDashboardDailyStatusProjection.Classify(recorded, fullLeave, halfLeave, working, incomplete).Status.ToString().ShouldBe(expected);
	}

	[Fact]
	public void Conflicting_Attendance_Should_Preserve_Presence_And_Explain_Review()
	{
		var (status, flags) = HRDashboardDailyStatusProjection.Classify(EmployeeAttendanceStatusType.Present, true, false, true);
		status.ShouldBe(HRDashboardDailyStatus.Present);
		flags.ShouldContain(x => x.Code == HRDashboardReviewFlagCode.FullDayLeaveConflict);
	}

	[Fact]
	public void Summary_Should_Reconcile_And_Count_Review_Separately()
	{
		var rows = HRDashboardDailyStatusProjection.Statuses.Select(status => new HRDashboardStaffDto
		{
			Status = Enum.Parse<HRDashboardDailyStatus>(status),
			ReviewFlags = status == "Present" ? [new(HRDashboardReviewFlagCode.FullDayLeaveConflict, "Conflict")] : []
		}).ToList();
		var counts = HRDashboardDailyStatusProjection.Count("2026-10-01", rows);
		(counts.Present + counts.Absent + counts.OnLeave + counts.HalfDay + counts.NotMarked + counts.NotScheduled + counts.NeedsSetup)
			.ShouldBe(counts.StaffCount);
		counts.StaffCount.ShouldBe(7);
		counts.NeedsReview.ShouldBe(1);
	}

	[Fact]
	public void Paging_Should_Filter_Before_Paging_With_Stable_Ties_For_More_Than_100_Staff()
	{
		var rows = Enumerable.Range(0, 150).Select(i => new HRDashboardStaffDto
		{
			EmployeeId = new Guid(i, 0, 0, new byte[8]), Name = "Asha Patel",
			EmployeeCode = $"STAFF-{i:D3}", Status = i % 2 == 0 ? HRDashboardDailyStatus.Present : HRDashboardDailyStatus.Absent
		}).Reverse().ToList();
		var input = new HRDashboardStaffInput { Status = "Present", SkipCount = 50, MaxResultCount = 25 };
		var page = HRDashboardDailyStatusProjection.Page(rows, input, "2026-10-01", DateTimeOffset.UtcNow);
		page.TotalCount.ShouldBe(75);
		page.Items.Count.ShouldBe(25);
		page.Items.First().EmployeeCode.ShouldBe("STAFF-100");
		page.Items.Last().EmployeeCode.ShouldBe("STAFF-148");
		input.Search = "staff-10";
		page = HRDashboardDailyStatusProjection.Page(rows, input, "2026-10-01", DateTimeOffset.UtcNow);
		page.TotalCount.ShouldBe(5);
		page.Items.ShouldBeEmpty();
	}
}
