using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Dashboard;

public interface IMainDashboardAppService : IApplicationService
{
	Task<DashboardSummaryDto> GetSummaryAsync(DashboardSummaryRequestDto input);
	Task<List<DashboardWidgetMetadataDto>> GetWidgetMetadataAsync();
}

public class DashboardSummaryRequestDto
{
	public string? PresentationRole { get; set; }
	public Guid? StudentId { get; set; }
	public string? DashboardTemplate { get; set; }
	public string? AcademicYearId { get; set; }
	public string? From { get; set; }
	public string? To { get; set; }
}

public class DashboardSummaryDto
{
	public string PresentationRole { get; set; } = string.Empty;
	public List<DashboardStudentOptionDto> Students { get; set; } = [];
	public Guid? SelectedStudentId { get; set; }
	public DashboardFocusDto? Focus { get; set; }
	public string DashboardTemplate { get; set; } = string.Empty;
	public string? AcademicYearId { get; set; }
	public DashboardDateRangeDto DateRange { get; set; } = new();
	public string ScopeLabel { get; set; } = string.Empty;
	public string DataState { get; set; } = "live";
	public DateTime GeneratedAtUtc { get; set; }
	public List<DashboardWidgetDto> KpiCards { get; set; } = [];
	public List<DashboardWidgetDto> ActionItems { get; set; } = [];
	public List<DashboardWidgetDto> RiskAlerts { get; set; } = [];
	public List<DashboardWidgetDto> CalendarItems { get; set; } = [];
	public List<DashboardWidgetDto> ModuleSnapshots { get; set; } = [];
	public List<DashboardWidgetDto> Announcements { get; set; } = [];
	public Dictionary<string, string> ZoneErrors { get; set; } = new();
}

public class DashboardDateRangeDto
{
	public string Label { get; set; } = string.Empty;
	public string? From { get; set; }
	public string? To { get; set; }
}

public class DashboardWidgetDto
{
	public List<string> RequiredPermissions { get; set; } = [];
	public string Id { get; set; } = string.Empty;
	public string Title { get; set; } = string.Empty;
	public object? Value { get; set; }
	public int? Count { get; set; }
	public string? DueLabel { get; set; }
	public string? Subtitle { get; set; }
	public string? Status { get; set; }
	public string? Trend { get; set; }
	public string? Owner { get; set; }
	public string? DrilldownUrl { get; set; }
	public string? Health { get; set; }
	public string Kind { get; set; } = "metric";
	public string Priority { get; set; } = "normal";
	public string? ActionLabel { get; set; }
}

public record DashboardStudentOptionDto(Guid Id, string Label);

public class DashboardFocusDto
{
	public string Kind { get; set; } = string.Empty;
	public string WidgetId { get; set; } = string.Empty;
	public string Title { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public string Status { get; set; } = "unavailable";
	public string? Unit { get; set; }
	public List<DashboardFocusSeriesDto> Series { get; set; } = [];
	public List<DashboardFocusItemDto> Items { get; set; } = [];
	public List<DashboardFocusMetricDto> Metrics { get; set; } = [];
	public string? Message { get; set; }
	public DateTime? AsOfUtc { get; set; }
	public string? PeriodLabel { get; set; }
	public string AccessMode { get; set; } = "staff";
	public List<string> RequiredPermissions { get; set; } = [];
}

public record DashboardFocusSeriesDto(string Id, string Label, List<DashboardFocusPointDto> Points);
public record DashboardFocusPointDto(string Key, string Label, decimal? Value);
public record DashboardFocusMetricDto(string Key, string Label, decimal? Value, string? Unit = null, string? Description = null);
public class DashboardFocusItemDto
{
	public string Id { get; set; } = string.Empty;
	public string Title { get; set; } = string.Empty;
	public string? Subtitle { get; set; }
	public string? Date { get; set; }
	public string? Time { get; set; }
	public string? EndTime { get; set; }
	public string? Status { get; set; }
	public string? DrilldownUrl { get; set; }
	public string? WidgetId { get; set; }
}

public class DashboardWidgetMetadataDto
{
	public string Id { get; set; } = string.Empty;
	public string Zone { get; set; } = string.Empty;
	public string DrilldownUrl { get; set; } = string.Empty;
	public List<string> RequiredPermissions { get; set; } = [];
	public string? Owner { get; set; }
}
