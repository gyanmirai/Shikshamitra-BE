using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Records;

public interface IStudentDashboardAppService : IApplicationService
{
	Task<StudentDashboardPreviewDto> GetPreviewAsync(string dashboardKey);
	Task<StudentDashboardPreviewDto> RefreshPreviewAsync(string dashboardKey);
}

public class StudentDashboardPreviewDto
{
	public string DashboardKey { get; set; } = string.Empty;
	public string ScopeKey { get; set; } = StudentDashboardScopeKeys.StudentWide;
	public string Status { get; set; } = StudentDashboardPreviewStatus.Empty;
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

[Serializable]
public class StudentDashboardPreviewRefreshJobArgs
{
	public Guid? TenantId { get; set; }
	public Guid PreviewId { get; set; }
	public string DashboardKey { get; set; } = string.Empty;
	public string ScopeKey { get; set; } = StudentDashboardScopeKeys.StudentWide;
	public Guid? RequestedByUserId { get; set; }
}

public class StudentReportingDashboardDto
{
	public DateTime GeneratedAt { get; set; }
	public DateTime RangeStart { get; set; }
	public DateTime RangeEnd { get; set; }
	public List<StudentReportingKpiDto> Kpis { get; set; } = [];
	public List<StudentReportingCategoryDto> Categories { get; set; } = [];
	public List<StudentReportingTrendPointDto> EnrollmentTrend { get; set; } = [];
	public List<StudentReportingAttendanceTrendPointDto> AttendanceTrend { get; set; } = [];
	public List<StudentReportingClassStrengthDto> ClassStrength { get; set; } = [];
	public List<StudentReportingAlertDto> Alerts { get; set; } = [];
	public List<StudentReportingQuickLinkDto> QuickLinks { get; set; } = [];
}

public class StudentReportingKpiDto
{
	public string Id { get; set; } = string.Empty;
	public string Label { get; set; } = string.Empty;
	public decimal Value { get; set; }
	public string? DisplayValue { get; set; }
	public string Tone { get; set; } = "default";
	public string? Description { get; set; }
	public string? Href { get; set; }
}

public class StudentReportingCategoryDto
{
	public string Id { get; set; } = string.Empty;
	public string Title { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public int AvailableReports { get; set; }
	public string Href { get; set; } = string.Empty;
	public string Icon { get; set; } = string.Empty;
}

public class StudentReportingTrendPointDto
{
	public string Label { get; set; } = string.Empty;
	public int Inquiries { get; set; }
	public int Registrations { get; set; }
	public int Admissions { get; set; }
	public int Enrolled { get; set; }
}

public class StudentReportingAttendanceTrendPointDto
{
	public string Date { get; set; } = string.Empty;
	public decimal Percentage { get; set; }
	public int Present { get; set; }
	public int Absent { get; set; }
}

public class StudentReportingClassStrengthDto
{
	public Guid? StandardId { get; set; }
	public string Standard { get; set; } = string.Empty;
	public int Total { get; set; }
	public List<StudentReportingDivisionStrengthDto> Divisions { get; set; } = [];
}

public class StudentReportingDivisionStrengthDto
{
	public Guid? DivisionId { get; set; }
	public string Division { get; set; } = string.Empty;
	public int Total { get; set; }
}

public class StudentReportingAlertDto
{
	public string Id { get; set; } = string.Empty;
	public string Severity { get; set; } = "info";
	public string Title { get; set; } = string.Empty;
	public string Summary { get; set; } = string.Empty;
	public int Count { get; set; }
	public string ActionLabel { get; set; } = string.Empty;
	public string ActionHref { get; set; } = string.Empty;
}

public class StudentReportingQuickLinkDto
{
	public string Id { get; set; } = string.Empty;
	public string Label { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public string Href { get; set; } = string.Empty;
	public string Icon { get; set; } = string.Empty;
}
