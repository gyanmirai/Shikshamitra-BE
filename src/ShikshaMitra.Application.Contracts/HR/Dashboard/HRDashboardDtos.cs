using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Dashboard;

public interface IHRDashboardAppService : IApplicationService
{
	Task<HRDashboardSummaryDto> GetSummaryAsync(HRDashboardSummaryRequestDto input);
	Task<HRDashboardPreviewDto> GetPreviewAsync(string dashboardKey);
	Task<HRDashboardPreviewDto> RefreshPreviewAsync(string dashboardKey);
}

public class HRDashboardSummaryRequestDto
{
	public string? Date { get; set; }
	public string? Month { get; set; }
	public int? Range { get; set; }
}

public class HRDashboardPreviewDto
{
	public string DashboardKey { get; set; } = string.Empty;
	public string ScopeKey { get; set; } = HrDashboardScopeKeys.HrWide;
	public string Status { get; set; } = HrDashboardPreviewStatus.Empty;
	public string PayloadJson { get; set; } = "{}";
	public int PayloadVersion { get; set; }
	public bool IsStale { get; set; }
	public DateTime? LastRefreshedAt { get; set; }
	public DateTime? LastSuccessfulRefreshAt { get; set; }
	public DateTime? NextScheduledRefreshAt { get; set; }
	public DateTime? RefreshStartedAt { get; set; }
	public Guid? RefreshRequestedByUserId { get; set; }
	public string? LastError { get; set; }
}

public class HRDashboardAttendancePreviewDto
{
	public DateTime Date { get; set; }
	public int TotalStaff { get; set; }
	public int Present { get; set; }
	public int Absent { get; set; }
	public int HalfDay { get; set; }
	public int MissingAttendance { get; set; }
	public decimal AttendanceRate { get; set; }
	public List<HRDashboardAttendanceTrendPointDto> Trend { get; set; } = [];
	public List<HRDashboardAttendanceDefaulterDto> TopDefaulters { get; set; } = [];
	public List<HRDashboardShiftSummaryDto> ShiftSummary { get; set; } = [];
}

public class HRDashboardAttendanceDefaulterDto
{
	public Guid EmployeeId { get; set; }
	public string EmployeeName { get; set; } = string.Empty;
	public string EmployeeIdentifier { get; set; } = string.Empty;
	public int MissingAttendance { get; set; }
	public int Absences { get; set; }
}

public class HRDashboardShiftSummaryDto
{
	public string ShiftName { get; set; } = string.Empty;
	public int StaffCount { get; set; }
	public int Present { get; set; }
	public int Missing { get; set; }
}

public class HRDashboardPayrollPreviewDto
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
}

public class HRDashboardEmployeeSnapshotDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public string EmployeeIdentifier { get; set; } = string.Empty;
	public string? RoleName { get; set; }
	public string? Department { get; set; }
	public bool IsTeaching { get; set; }
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
