using System;
using System.Collections.Generic;

namespace ShikshaMitra.HR.Dashboard;

internal static class HRDashboardProjection
{
	public static HRDashboardAttendancePreviewDto BuildAttendancePreview(HRDashboardSummaryDto summary) =>
		HRDashboardPreviewProjection.BuildAttendancePreview(summary);

	public static HRDashboardPayrollPreviewDto BuildPayrollPreview(HRDashboardSummaryDto summary) =>
		HRDashboardPreviewProjection.BuildPayrollPreview(summary);

	public static HRDashboardAttendanceTodayDto BuildAttendanceToday(
		IReadOnlyCollection<AttendanceStatusCount> attendanceCounts,
		DateOnly today,
		int activeStaffCount) =>
		HRDashboardAttendanceProjection.BuildAttendanceToday(attendanceCounts, today, activeStaffCount);

	public static List<HRDashboardAttendanceTrendPointDto> BuildAttendanceTrend(
		IReadOnlyCollection<AttendanceStatusCount> attendanceCounts,
		IReadOnlyCollection<LeaveTrendRow> approvedLeaves,
		DateOnly trendStart,
		DateOnly trendEnd) =>
		HRDashboardAttendanceProjection.BuildAttendanceTrend(attendanceCounts, approvedLeaves, trendStart, trendEnd);

	public static HRDashboardCelebrationsDto BuildCelebrations(
		IReadOnlyCollection<ActiveEmployeeDashboardRow> employees,
		DateTime dashboardDate) =>
		HRDashboardCelebrationProjection.BuildCelebrations(employees, dashboardDate);

	public static List<HRDashboardInsightDto> BuildInsights(
		HRDashboardAttendanceTodayDto attendanceToday,
		HRDashboardLeaveDto leave,
		HRDashboardPayrollDto payroll,
		HRDashboardCelebrationsDto celebrations,
		InactiveReportingManagerIssue inactiveReportingManagerIssue) =>
		HRDashboardInsightProjection.BuildInsights(attendanceToday, leave, payroll, celebrations, inactiveReportingManagerIssue);
}
