using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.QuestionBank;

[RemoteService(IsEnabled = false)]
public interface IQuestionBankAppService : IApplicationService
{
    Task<QuestionBankSettingDto> GetSettingsAsync(Guid academicYearId);
    Task<QuestionBankSettingDto> SaveSettingsAsync(QuestionBankSettingDto input);
    Task<QuestionListResponseDto> GetQuestionsAsync(QuestionSearchInput input);
    Task<QuestionBankItemDto> GetQuestionAsync(Guid id);
    Task<QuestionBankItemDto> CreateQuestionAsync(CreateUpdateQuestionDto input);
    Task<QuestionBankItemDto> UpdateQuestionAsync(Guid id, CreateUpdateQuestionDto input);
    Task DeleteQuestionAsync(Guid id);
    Task<QuestionBankItemDto> SubmitQuestionAsync(Guid id);
    Task<QuestionBankItemDto> ApproveQuestionAsync(Guid id, QuestionReviewDto input);
    Task<QuestionBankItemDto> RejectQuestionAsync(Guid id, QuestionReviewDto input);
    Task<QuestionBankItemDto> ArchiveQuestionAsync(Guid id);
    Task<QuestionBankItemDto> DuplicateQuestionAsync(Guid id);
    Task<DuplicateQuestionResultDto> CheckDuplicateAsync(CheckDuplicateQuestionDto input);
    Task<BulkQuestionWorkflowResultDto> BulkApproveAsync(BulkQuestionReviewDto input);
    Task<BulkQuestionWorkflowResultDto> BulkRejectAsync(BulkQuestionReviewDto input);
    Task<QuestionBankDashboardDto> GetDashboardAsync(Guid academicYearId);
}

public class QuestionBankSettingDto
{
    public Guid? Id { get; set; }
    public Guid AcademicYearId { get; set; }
    public QuestionApprovalMode ApprovalMode { get; set; } = QuestionApprovalMode.HodApproval;
    public QuestionVisibility DefaultVisibility { get; set; } = QuestionVisibility.Department;
    public bool EnableMcq { get; set; } = true;
    public bool EnableTrueFalse { get; set; } = true;
    public bool EnableFillInTheBlank { get; set; } = true;
    public bool EnableSubjective { get; set; } = true;
    public bool EnableMatchTheFollowing { get; set; } = true;
    public bool EnableAssertionReason { get; set; } = true;
    public bool EnableCaseStudy { get; set; } = true;
    public bool EnableDiagramBased { get; set; } = true;
    public bool RequireChapter { get; set; } = true;
    public bool RequireTopic { get; set; }
    public bool RequireDifficulty { get; set; } = true;
    public bool RequireBloomLevel { get; set; }
    public bool RequireAnswerKey { get; set; } = true;
    public bool RequireLearningOutcome { get; set; }
    public bool AllowDecimalMarks { get; set; }
    public bool EnableNegativeMarking { get; set; }
    public bool EnableDuplicateCheck { get; set; } = true;
    public DuplicateQuestionAction DuplicateQuestionAction { get; set; } = DuplicateQuestionAction.AllowWithWarning;
    public bool EnableQuestionVersioning { get; set; } = true;
    public bool EnableBulkImport { get; set; } = true;
    public bool EnableRichTextEditor { get; set; } = true;
    public bool EnableLatexEquation { get; set; }
    public bool EnableImageAttachment { get; set; } = true;
    public int MaxAttachmentSizeMb { get; set; } = 5;
    public bool EnableAiQuestionGenerator { get; set; }
    public bool EnableAiBloomClassifier { get; set; }
    public bool EnableAiDuplicateDetector { get; set; }
}

public class QuestionOptionDto
{
    public Guid? Id { get; set; }
    public string OptionText { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public int SortOrder { get; set; }
}

public class QuestionBankItemDto
{
    public Guid Id { get; set; }
    public Guid AcademicYearId { get; set; }
    public Guid? BoardId { get; set; }
    public Guid? MediumId { get; set; }
    public Guid StandardId { get; set; }
    public Guid SubjectId { get; set; }
    public Guid? ChapterId { get; set; }
    public Guid? TopicId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public QuestionType QuestionType { get; set; }
    public DifficultyLevel DifficultyLevel { get; set; }
    public BloomLevel? BloomLevel { get; set; }
    public decimal Marks { get; set; }
    public decimal? NegativeMarks { get; set; }
    public string? AnswerKey { get; set; }
    public string? LearningOutcome { get; set; }
    public string? Explanation { get; set; }
    public string? MarkingScheme { get; set; }
    public QuestionStatus Status { get; set; }
    public QuestionVisibility Visibility { get; set; }
    public Guid CreatedByTeacherId { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewComment { get; set; }
    public int VersionNumber { get; set; }
    public Guid? ParentQuestionId { get; set; }
    public bool IsLocked { get; set; }
    public bool IsArchived { get; set; }
    public int UsedCount { get; set; }
    public List<QuestionOptionDto> Options { get; set; } = new();
    public List<string> Tags { get; set; } = new();
}

public class CreateUpdateQuestionDto
{
    public Guid AcademicYearId { get; set; }
    public Guid? BoardId { get; set; }
    public Guid? MediumId { get; set; }
    public Guid StandardId { get; set; }
    public Guid SubjectId { get; set; }
    public Guid? ChapterId { get; set; }
    public Guid? TopicId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public QuestionType QuestionType { get; set; }
    public DifficultyLevel DifficultyLevel { get; set; }
    public BloomLevel? BloomLevel { get; set; }
    public decimal Marks { get; set; }
    public decimal? NegativeMarks { get; set; }
    public string? AnswerKey { get; set; }
    public string? LearningOutcome { get; set; }
    public string? Explanation { get; set; }
    public string? MarkingScheme { get; set; }
    public QuestionVisibility? Visibility { get; set; }
    public List<QuestionOptionDto> Options { get; set; } = new();
    public List<string> Tags { get; set; } = new();
}

public class QuestionSearchInput
{
    public Guid? AcademicYearId { get; set; }
    public Guid? StandardId { get; set; }
    public Guid? SubjectId { get; set; }
    public Guid? ChapterId { get; set; }
    public Guid? TopicId { get; set; }
    public QuestionType? QuestionType { get; set; }
    public DifficultyLevel? DifficultyLevel { get; set; }
    public BloomLevel? BloomLevel { get; set; }
    public decimal? Marks { get; set; }
    public QuestionStatus? Status { get; set; }
    public QuestionVisibility? Visibility { get; set; }
    public string? Keyword { get; set; }
    public Guid? CreatedByTeacherId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class QuestionListResponseDto
{
    public int TotalCount { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public List<QuestionBankItemDto> Items { get; set; } = new();
}

public class QuestionReviewDto
{
    public string? Comment { get; set; }
}

public class CheckDuplicateQuestionDto
{
    public Guid AcademicYearId { get; set; }
    public Guid StandardId { get; set; }
    public Guid SubjectId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public Guid? ExcludeQuestionId { get; set; }
}

public class DuplicateQuestionResultDto
{
    public bool HasDuplicate { get; set; }
    public bool IsBlocked { get; set; }
    public string? Message { get; set; }
    public List<QuestionBankItemDto> Matches { get; set; } = new();
}

public class BulkQuestionReviewDto
{
    public List<Guid> QuestionIds { get; set; } = new();
    public string? Comment { get; set; }
}

public class BulkQuestionWorkflowResultDto
{
    public int UpdatedCount { get; set; }
}

public class QuestionBankDashboardDto
{
    public int TotalQuestions { get; set; }
    public int ApprovedQuestions { get; set; }
    public int DraftQuestions { get; set; }
    public int PendingReview { get; set; }
    public int RejectedQuestions { get; set; }
}
