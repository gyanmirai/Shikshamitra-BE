namespace ShikshaMitra.HR.Dashboard;

internal static class HRDashboardPreviewProjection
{
	public static HRDashboardAttendancePreviewDto BuildAttendancePreview(HRDashboardSummaryDto summary)
	{
		var totalStaff = summary.Workforce.TotalActiveStaff;
		var missing = summary.AttendanceToday.MissingAttendance;
		return new HRDashboardAttendancePreviewDto
		{
			Date = summary.Date,
			TotalStaff = totalStaff,
			Present = summary.AttendanceToday.Present,
			Absent = summary.AttendanceToday.Absent,
			HalfDay = summary.AttendanceToday.HalfDay,
			MissingAttendance = missing,
			AttendanceRate = summary.AttendanceToday.AttendanceRate,
			Trend = summary.AttendanceTrend,
			ShiftSummary =
			[
				new HRDashboardShiftSummaryDto
				{
					ShiftName = "All Staff",
					StaffCount = totalStaff,
					Present = summary.AttendanceToday.Present,
					Missing = missing
				}
			]
		};
	}

	public static HRDashboardPayrollPreviewDto BuildPayrollPreview(HRDashboardSummaryDto summary)
	{
		var payroll = summary.Payroll;
		return new HRDashboardPayrollPreviewDto
		{
			Month = payroll.Month,
			CanPreview = payroll.CanPreview,
			CanLock = payroll.CanLock,
			EmployeeCount = payroll.EmployeeCount,
			ReviewedCount = payroll.ReviewedCount,
			LockedCount = payroll.LockedCount,
			PendingCount = payroll.PendingCount,
			MissingAttendanceCount = payroll.MissingAttendanceCount,
			MissingSalaryStructureCount = payroll.MissingSalaryStructureCount,
			PolicyConfigurationIssueCount = payroll.PolicyConfigurationIssueCount,
			EstimatedGrossAmount = payroll.EstimatedGrossAmount,
			EstimatedNetAmount = payroll.EstimatedNetAmount,
			BlockingIssues = payroll.BlockingIssues,
			RecommendedActions = payroll.RecommendedActions
		};
	}
}
