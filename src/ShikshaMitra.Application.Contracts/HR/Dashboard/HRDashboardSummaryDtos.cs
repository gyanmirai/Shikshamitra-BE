using System;
using System.Collections.Generic;

namespace ShikshaMitra.HR.Dashboard;

public class HRDashboardSummaryDto
{
	public DateTime Date { get; set; }
	public string Month { get; set; } = string.Empty;
	public int Range { get; set; }
	public HRDashboardWorkforceDto Workforce { get; set; } = new();
	public HRDashboardAttendanceTodayDto AttendanceToday { get; set; } = new();
	public List<HRDashboardAttendanceTrendPointDto> AttendanceTrend { get; set; } = [];
	public HRDashboardLeaveDto Leave { get; set; } = new();
	public HRDashboardPayrollDto Payroll { get; set; } = new();
	public HRDashboardCelebrationsDto Celebrations { get; set; } = new();
	public List<HRDashboardInsightDto> Insights { get; set; } = [];
}

public class HRDashboardWorkforceDto
{
	public int TotalActiveStaff { get; set; }
	public int TeachingStaff { get; set; }
	public int NonTeachingStaff { get; set; }
	public int NewJoinersThisMonth { get; set; }
	public int InactiveStaff { get; set; }
	public List<HRDashboardEmployeeSnapshotDto> RecentEmployees { get; set; } = [];
	public List<HRDashboardEmployeeSnapshotDto> NewJoiners { get; set; } = [];
}

public class HRDashboardEmployeeSnapshotDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public string EmployeeIdentifier { get; set; } = string.Empty;
	public string? RoleName { get; set; }
	public string? Department { get; set; }
	public bool IsTeaching { get; set; }
	public DateOnly? DateOfJoining { get; set; }
}

public class HRDashboardAttendanceTodayDto
{
	public int Present { get; set; }
	public int Absent { get; set; }
	public int HalfDay { get; set; }
	public int MissingAttendance { get; set; }
	public int LateOrException { get; set; }
	public decimal AttendanceRate { get; set; }
}

public class HRDashboardAttendanceTrendPointDto
{
	public DateTime Date { get; set; }
	public int Present { get; set; }
	public int Absent { get; set; }
	public int Leave { get; set; }
}

public class HRDashboardLeaveDto
{
	public int Pending { get; set; }
	public int Approved { get; set; }
	public int Rejected { get; set; }
	public List<HRDashboardLeaveTypeSummaryDto> LeaveTypeSummary { get; set; } = [];
	public List<HRDashboardLeaveRequestDto> RecentPendingRequests { get; set; } = [];
}

public class HRDashboardLeaveTypeSummaryDto
{
	public string LeaveType { get; set; } = string.Empty;
	public int Count { get; set; }
}

public class HRDashboardLeaveRequestDto
{
	public Guid Id { get; set; }
	public int PublicId { get; set; }
	public Guid EmployeeId { get; set; }
	public string EmployeeName { get; set; } = string.Empty;
	public string EmployeeIdentifier { get; set; } = string.Empty;
	public string LeaveType { get; set; } = string.Empty;
	public DateTime StartDate { get; set; }
	public DateTime EndDate { get; set; }
	public decimal TotalDays { get; set; }
	public string Status { get; set; } = string.Empty;
	public DateTime SubmittedAt { get; set; }
}

public class HRDashboardPayrollDto
{
	public string Month { get; set; } = string.Empty;
	public bool CanPreview { get; set; }
	public bool CanLock { get; set; }
	public int EmployeeCount { get; set; }
	public int ReviewedCount { get; set; }
	public int LockedCount { get; set; }
	public int PendingCount { get; set; }
	public int MissingAttendanceCount { get; set; }
	public int MissingSalaryStructureCount { get; set; }
	public int PolicyConfigurationIssueCount { get; set; }
	public decimal? EstimatedGrossAmount { get; set; }
	public decimal? EstimatedNetAmount { get; set; }
	public List<string> BlockingIssues { get; set; } = [];
	public List<string> RecommendedActions { get; set; } = [];
}

public class HRDashboardCelebrationsDto
{
	public List<HRDashboardCelebrationDto> Today { get; set; } = [];
	public List<HRDashboardCelebrationDto> Upcoming { get; set; } = [];
}

public class HRDashboardCelebrationDto
{
	public Guid EmployeeId { get; set; }
	public string EmployeeName { get; set; } = string.Empty;
	public string EmployeeIdentifier { get; set; } = string.Empty;
	public string Type { get; set; } = string.Empty;
	public DateTime Date { get; set; }
	public int? Years { get; set; }
}

public class HRDashboardInsightDto
{
	public string Severity { get; set; } = "info";
	public string Title { get; set; } = string.Empty;
	public string Summary { get; set; } = string.Empty;
	public string Source { get; set; } = string.Empty;
	public string ActionLabel { get; set; } = string.Empty;
	public string ActionHref { get; set; } = string.Empty;
}
