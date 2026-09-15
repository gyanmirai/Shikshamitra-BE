using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.Reports;

public class HrAiReportPlanRequestDto
{
	[Required]
	[StringLength(2000, MinimumLength = 10)]
	public string Prompt { get; set; } = string.Empty;

	[MaxLength(6)]
	public List<HrAiReportClarificationAnswerDto> ClarificationAnswers { get; set; } = [];
}

public class HrAiReportClarificationAnswerDto
{
	[Required]
	[StringLength(300, MinimumLength = 1)]
	public string Question { get; set; } = string.Empty;

	[Required]
	[StringLength(500, MinimumLength = 1)]
	public string Answer { get; set; } = string.Empty;
}

public class HrAiReportClarificationQuestionDto
{
	public string Id { get; set; } = string.Empty;
	public string Question { get; set; } = string.Empty;
	public List<string> Options { get; set; } = [];
	public string RecommendedOption { get; set; } = string.Empty;
}

public class HrAiReportPlanResponseDto
{
	public string Status { get; set; } = HrAiReportPlanStatuses.Ready;
	public HrReportRequestDto? Plan { get; set; }
	public string InterpretedRequest { get; set; } = string.Empty;
	public string Summary { get; set; } = string.Empty;
	public List<HrAiReportClarificationQuestionDto> ClarificationQuestions { get; set; } = [];
	public List<string> Assumptions { get; set; } = [];
	public double Confidence { get; set; }
	public string PermissionSummary { get; set; } = string.Empty;
}

public static class HrAiReportPlanStatuses
{
	public const string NeedsClarification = "NeedsClarification";
	public const string Ready = "Ready";
}
