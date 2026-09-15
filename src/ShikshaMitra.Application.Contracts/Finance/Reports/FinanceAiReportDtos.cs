using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Finance.Reports;

public class FinanceAiReportPlanRequestDto
{
    [Required]
    [StringLength(2000, MinimumLength = 10)]
    public string Prompt { get; set; } = string.Empty;

    [MaxLength(6)]
    public List<FinanceAiReportClarificationAnswerDto> ClarificationAnswers { get; set; } = [];
}

public class FinanceAiReportClarificationAnswerDto
{
    [Required]
    [StringLength(300, MinimumLength = 1)]
    public string Question { get; set; } = string.Empty;

    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string Answer { get; set; } = string.Empty;
}

public class FinanceAiReportClarificationQuestionDto
{
    public string Id { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public List<string> Options { get; set; } = [];
    public string RecommendedOption { get; set; } = string.Empty;
}

public class FinanceAiReportPlanResponseDto
{
    public string Status { get; set; } = FinanceAiReportPlanStatuses.Ready;
    public FinanceReportRequestDto? Plan { get; set; }
    public string InterpretedRequest { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public List<FinanceAiReportClarificationQuestionDto> ClarificationQuestions { get; set; } = [];
    public List<string> Assumptions { get; set; } = [];
    public double Confidence { get; set; }
    public string PermissionSummary { get; set; } = string.Empty;
}

public static class FinanceAiReportPlanStatuses
{
    public const string NeedsClarification = "NeedsClarification";
    public const string Ready = "Ready";
}
