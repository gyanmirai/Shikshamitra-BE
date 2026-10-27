using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Reports;
using Volo.Abp.Application.Services;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.TenantSettings.Reports;

public class InstituteReportRequest
{
	[Required] public string ReportType { get; set; } = "academic-hierarchy";
	public string Format { get; set; } = "pdf";
	public int Page { get; set; } = 1;
	public int PageSize { get; set; } = 25;
	public InstituteReportFilters Filters { get; set; } = new();
	public List<string> Columns { get; set; } = [];
	public List<InstituteReportCondition> Conditions { get; set; } = [];
	public string? SortField { get; set; }
	public bool Descending { get; set; }
}

public class InstituteReportFilters
{
	public Guid? AcademicYearId { get; set; }
	public Guid? BoardId { get; set; }
	public Guid? MediumId { get; set; }
	public Guid? StageId { get; set; }
	public Guid? StandardId { get; set; }
	public Guid? DivisionId { get; set; }
	public Guid? BuildingId { get; set; }
	public Guid? FloorId { get; set; }
	public Guid? RoomTypeId { get; set; }
	public Guid? ShiftId { get; set; }
	public string? RoomAssignment { get; set; }
	public string? DocumentCategory { get; set; }
	public string? IdentityScope { get; set; }
}

public class InstituteReportCondition
{
	public string Field { get; set; } = "";
	public string Operator { get; set; } = "contains";
	public string Value { get; set; } = "";
}

public record InstituteReportDefinition(string Id, string Label, bool Academic, List<AcademicReportColumnDto> Columns)
{
	public string[] RequiredPermissions { get; init; } = [];
}
public record InstituteReportOption(Guid Id, string Name, Guid? ParentId = null);
public record InstituteReportValue(string Value, string Label);
public class InstituteReportOptions
{
	public List<InstituteReportDefinition> ReportTypes { get; set; } = [];
	public Dictionary<string, List<InstituteReportOption>> Choices { get; set; } = [];
	public Dictionary<string, List<InstituteReportValue>> Values { get; set; } = [];
}

public class InstituteReportJobDto
{
	public Guid Id { get; set; }
	public string ReportType { get; set; } = "";
	public string Format { get; set; } = "";
	public string Status { get; set; } = "";
	public string? FileName { get; set; }
	public string? ErrorMessage { get; set; }
	public DateTime CreationTime { get; set; }
	public DateTime? CompletedAt { get; set; }
	public DateTime ExpiresAt { get; set; }
	public InstituteReportRequest Request { get; set; } = new();
	public bool IsDynamic { get; set; }
}

public class InstituteReportHistoryRowDto
{
	public Guid Id { get; set; }
	public string ReportTypeId { get; set; } = "";
	public string ReportType { get; set; } = "";
	public string Format { get; set; } = "";
	public string Status { get; set; } = "";
	public string? FileName { get; set; }
	public string? ErrorMessage { get; set; }
	public DateTime CreationTime { get; set; }
	public DateTime? CompletedAt { get; set; }
	public DateTime ExpiresAt { get; set; }
	public bool IsDynamic { get; set; }
}

public class InstituteReportHistoryInput : PagedAndSortedResultRequestDto
{
	[StringLength(200)] public string? Filter { get; set; }
	[StringLength(16384)] public string? ColumnFilters { get; set; }
	[StringLength(100)] public string? TimeZone { get; set; }
}

public class InstituteAiPlanInput
{
	[Required, StringLength(2000, MinimumLength = 10)] public string Prompt { get; set; } = "";
	public Guid? AcademicYearId { get; set; }
	public List<AcademicAiReportClarificationAnswerDto> ClarificationAnswers { get; set; } = [];
}

public class InstituteAiPlanResult
{
	public string Status { get; set; } = "Ready";
	public string Summary { get; set; } = "";
	public InstituteReportRequest? Plan { get; set; }
	public List<AcademicAiReportClarificationQuestionDto> ClarificationQuestions { get; set; } = [];
	public string? ApprovalToken { get; set; }
}

public class InstituteAiApprovedRequest
{
	public InstituteReportRequest Plan { get; set; } = new();
	public string ApprovalToken { get; set; } = "";
}

public interface IInstituteReportAppService : IApplicationService
{
	Task<InstituteReportOptions> GetOptionsAsync();
	Task<AcademicReportPreviewDto> PreviewAsync(InstituteReportRequest input);
	Task<InstituteReportJobDto> CreateExportAsync(InstituteReportRequest input);
	Task<List<InstituteReportJobDto>> GetExportsAsync();
	Task<PagedResultDto<InstituteReportHistoryRowDto>> GetHistoryAsync(InstituteReportHistoryInput input);
	Task<List<InstituteReportHistoryRowDto>> GetStatusesAsync(List<Guid> ids);
	Task<InstituteReportJobDto> GetExportAsync(Guid jobId);
	Task<AcademicReportDownloadDto> DownloadAsync(Guid jobId);
	Task<InstituteAiPlanResult> PlanAsync(InstituteAiPlanInput input);
	Task<AcademicReportPreviewDto> AiPreviewAsync(InstituteAiApprovedRequest input);
	Task<InstituteReportJobDto> AiExportAsync(InstituteAiApprovedRequest input);
}
