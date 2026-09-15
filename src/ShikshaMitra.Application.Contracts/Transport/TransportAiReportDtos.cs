using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Transport;

public class TransportAiReportPlanRequestDto
{
	[Required]
	[StringLength(2000, MinimumLength = 10)]
	public string Prompt { get; set; } = string.Empty;

	[Required]
	public System.Guid AcademicMediumId { get; set; }

	public System.Guid? AcademicYearId { get; set; }

	[MaxLength(6)]
	public List<TransportAiReportClarificationAnswerDto> ClarificationAnswers { get; set; } = [];
}

public class TransportAiReportClarificationAnswerDto
{
	[Required]
	[StringLength(300, MinimumLength = 1)]
	public string Question { get; set; } = string.Empty;

	[Required]
	[StringLength(500, MinimumLength = 1)]
	public string Answer { get; set; } = string.Empty;
}

public class TransportAiReportClarificationQuestionDto
{
	public string Id { get; set; } = string.Empty;
	public string Question { get; set; } = string.Empty;
	public List<string> Options { get; set; } = [];
	public string RecommendedOption { get; set; } = string.Empty;
}

public class TransportAiReportExecutionPlanDto
{
	public string ReportKey { get; set; } = string.Empty;
	public string Format { get; set; } = "csv";
	public TransportReportListRequestDto Query { get; set; } = new();
}

public class TransportAiReportPlanResponseDto
{
	public string Status { get; set; } = TransportAiReportPlanStatuses.Ready;
	public TransportAiReportExecutionPlanDto? Plan { get; set; }
	public string InterpretedRequest { get; set; } = string.Empty;
	public string Summary { get; set; } = string.Empty;
	public List<TransportAiReportClarificationQuestionDto> ClarificationQuestions { get; set; } = [];
	public List<string> Assumptions { get; set; } = [];
	public double Confidence { get; set; }
	public string PermissionSummary { get; set; } = string.Empty;
}

public static class TransportAiReportPlanStatuses
{
	public const string NeedsClarification = "NeedsClarification";
	public const string Ready = "Ready";
}
