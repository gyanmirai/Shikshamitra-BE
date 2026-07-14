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
	public string? DashboardTemplate { get; set; }
	public string? AcademicYearId { get; set; }
	public string? From { get; set; }
	public string? To { get; set; }
}

public class DashboardSummaryDto
{
	public string DashboardTemplate { get; set; } = string.Empty;
	public string? AcademicYearId { get; set; }
	public DashboardDateRangeDto DateRange { get; set; } = new();
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
}

public class DashboardWidgetMetadataDto
{
	public string Id { get; set; } = string.Empty;
	public string Zone { get; set; } = string.Empty;
	public string DrilldownUrl { get; set; } = string.Empty;
	public List<string> RequiredPermissions { get; set; } = [];
	public string? Owner { get; set; }
}
