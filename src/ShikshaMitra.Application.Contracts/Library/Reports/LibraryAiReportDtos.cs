using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Library.Reports;

public class LibraryAiReportPlanRequestDto
{
	[Required]
	[StringLength(2000, MinimumLength = 10)]
	public string Prompt { get; set; } = string.Empty;

	[MaxLength(6)]
	public List<LibraryAiReportClarificationAnswerDto> ClarificationAnswers { get; set; } = [];
}

public class LibraryAiReportClarificationAnswerDto
{
	[Required]
	[StringLength(300, MinimumLength = 1)]
	public string Question { get; set; } = string.Empty;

	[Required]
	[StringLength(500, MinimumLength = 1)]
	public string Answer { get; set; } = string.Empty;
}

public class LibraryAiReportClarificationQuestionDto
{
	public string Id { get; set; } = string.Empty;
	public string Question { get; set; } = string.Empty;
	public List<string> Options { get; set; } = [];
	public string RecommendedOption { get; set; } = string.Empty;
}

public class LibraryAiReportPlanResponseDto
{
	public string Status { get; set; } = LibraryAiReportPlanStatuses.Ready;
	public LibraryReportRequestDto? Plan { get; set; }
	public string InterpretedRequest { get; set; } = string.Empty;
	public string Summary { get; set; } = string.Empty;
	public List<LibraryAiReportClarificationQuestionDto> ClarificationQuestions { get; set; } = [];
	public List<string> Assumptions { get; set; } = [];
	public double Confidence { get; set; }
	public string PermissionSummary { get; set; } = string.Empty;
}

public static class LibraryAiReportPlanStatuses
{
	public const string NeedsClarification = "NeedsClarification";
	public const string Ready = "Ready";
}
