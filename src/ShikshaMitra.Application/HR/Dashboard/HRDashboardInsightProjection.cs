using System.Collections.Generic;

namespace ShikshaMitra.HR.Dashboard;

internal static class HRDashboardInsightProjection
{
	public static List<HRDashboardInsightDto> BuildInsights(
		HRDashboardAttendanceTodayDto attendanceToday,
		HRDashboardLeaveDto leave,
		HRDashboardPayrollDto payroll,
		HRDashboardCelebrationsDto celebrations,
		InactiveReportingManagerIssue inactiveReportingManagerIssue)
	{
		var insights = new List<HRDashboardInsightDto>();

		if (inactiveReportingManagerIssue.ActiveDirectReportCount > 0)
		{
			insights.Add(new HRDashboardInsightDto
			{
				Severity = "critical",
				Title = "Inactive reporting managers",
				Summary = $"{inactiveReportingManagerIssue.ActiveDirectReportCount} active employee(s) report to {inactiveReportingManagerIssue.ManagerCount} inactive manager(s).",
				Source = "employee.reporting-manager",
				ActionLabel = "Review employees",
				ActionHref = "/hr/employee?reportingManagerStatus=nonActive"
			});
		}

		if (payroll.MissingAttendanceCount > 0)
		{
			insights.Add(new HRDashboardInsightDto
			{
				Severity = "warning",
				Title = "Attendance missing for payroll",
				Summary = $"{payroll.MissingAttendanceCount} staff members are missing payroll attendance inputs for {payroll.Month}.",
				Source = "payroll.readiness",
				ActionLabel = "Review attendance",
				ActionHref = "/hr/manual-attendance"
			});
		}

		if (payroll.MissingSalaryStructureCount > 0)
		{
			insights.Add(new HRDashboardInsightDto
			{
				Severity = "critical",
				Title = "Salary structures incomplete",
				Summary = $"{payroll.MissingSalaryStructureCount} staff members need salary structures before payroll can lock.",
				Source = "payroll.readiness",
				ActionLabel = "Open salary structures",
				ActionHref = "/hr/payroll/structure"
			});
		}

		if (!payroll.CanLock && payroll.BlockingIssues.Count > 0)
		{
			insights.Add(new HRDashboardInsightDto
			{
				Severity = "critical",
				Title = "Payroll cannot be locked",
				Summary = payroll.BlockingIssues[0],
				Source = "payroll.readiness",
				ActionLabel = "Open payroll",
				ActionHref = "/hr/payroll/run-payroll"
			});
		}

		if (leave.Pending > 0)
		{
			insights.Add(new HRDashboardInsightDto
			{
				Severity = leave.Pending >= 5 ? "warning" : "info",
				Title = "Leave approvals pending",
				Summary = $"{leave.Pending} leave requests are waiting for HR action.",
				Source = "leave.management",
				ActionLabel = "Review leaves",
				ActionHref = "/hr/leave-approvals"
			});
		}

		if (attendanceToday.AttendanceRate > 0 && attendanceToday.AttendanceRate < 85)
		{
			insights.Add(new HRDashboardInsightDto
			{
				Severity = "warning",
				Title = "Attendance below target",
				Summary = $"Today's staff attendance is {attendanceToday.AttendanceRate:0.##}%.",
				Source = "attendance.today",
				ActionLabel = "View attendance",
				ActionHref = "/hr/attendance-tracking"
			});
		}

		if (celebrations.Today.Count > 0)
		{
			insights.Add(new HRDashboardInsightDto
			{
				Severity = "info",
				Title = "Celebrations today",
				Summary = $"{celebrations.Today.Count} birthday or work anniversary is due today.",
				Source = "employee.celebrations",
				ActionLabel = "View celebrations",
				ActionHref = "/hr/dashboard"
			});
		}

		return insights;
	}
}
