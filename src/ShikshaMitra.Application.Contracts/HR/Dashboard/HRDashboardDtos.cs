using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Dashboard;

public interface IHRDashboardAppService : IApplicationService
{
	Task<HRDashboardOverviewDto> GetOverviewAsync(HRDashboardOverviewInput input);
	Task<HRDashboardStaffPageDto> GetStaffAsync(HRDashboardStaffInput input);
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
