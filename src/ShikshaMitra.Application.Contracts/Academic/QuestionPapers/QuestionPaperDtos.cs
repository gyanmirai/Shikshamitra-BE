using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.Academic.QuestionBank;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.QuestionPapers;

[RemoteService(IsEnabled = false)]
public interface IQuestionPaperAppService : IApplicationService
{
    Task<QuestionPaperSettingDto> GetSettingsAsync(Guid academicYearId);
    Task<QuestionPaperSettingDto> SaveSettingsAsync(QuestionPaperSettingDto input);
    Task<QuestionPaperListResponseDto> GetPapersAsync(QuestionPaperSearchInput input);
    Task<QuestionPaperDto> GetPaperAsync(Guid id);
    Task<QuestionPaperDto> CreatePaperAsync(CreateUpdateQuestionPaperDto input);
    Task<QuestionPaperDto> UpdatePaperAsync(Guid id, CreateUpdateQuestionPaperDto input);
    Task DeletePaperAsync(Guid id);
    Task<QuestionPaperDto> SubmitAsync(Guid id);
    Task<QuestionPaperDto> ApproveAsync(Guid id, PaperReviewDto input);
    Task<QuestionPaperDto> RejectAsync(Guid id, PaperReviewDto input);
    Task<QuestionPaperDto> PublishAsync(Guid id);
    Task<QuestionPaperDto> LockAsync(Guid id);
    Task<QuestionPaperDto> ArchiveAsync(Guid id);
    Task<QuestionPaperBlueprintDto?> GetBlueprintAsync(Guid paperId);
    Task<QuestionPaperBlueprintDto> SaveBlueprintAsync(Guid paperId, QuestionPaperBlueprintDto input);
    Task<BlueprintValidationResultDto> ValidateBlueprintAsync(Guid paperId);
    Task<QuestionPaperDto> GenerateAsync(Guid paperId);
    Task<QuestionPaperDto> AddQuestionAsync(Guid paperId, AddPaperQuestionDto input);
    Task<QuestionPaperDto> RemoveQuestionAsync(Guid paperId, Guid paperQuestionId);
    Task<QuestionPaperDto> ReorderQuestionsAsync(Guid paperId, ReorderPaperQuestionsDto input);
    Task<QuestionPaperPreviewDto> GetPreviewAsync(Guid paperId);
    Task<QuestionPaperDashboardDto> GetDashboardAsync(Guid academicYearId);
}

public class QuestionPaperSettingDto
{
    public Guid? Id { get; set; }
    public Guid AcademicYearId { get; set; }
    public bool RequireApprovedQuestions { get; set; } = true;
    public bool EnableBlueprintValidation { get; set; } = true;
    public bool EnableRecentQuestionBlock { get; set; }
    public int RecentQuestionBlockDays { get; set; } = 90;
    public decimal DistributionTolerancePercentage { get; set; } = 5;
    public bool EnablePdfCompilation { get; set; } = true;
    public bool LockAfterPublish { get; set; } = true;
    public bool EnableAiBlueprintGenerator { get; set; }
    public bool EnableAiPaperGenerator { get; set; }
}

public class QuestionPaperDto
{
    public Guid Id { get; set; }
    public Guid AcademicYearId { get; set; }
    public Guid? ExamId { get; set; }
    public Guid? ExamScheduleId { get; set; }
    public Guid StandardId { get; set; }
    public Guid SubjectId { get; set; }
    public string PaperTitle { get; set; } = string.Empty;
    public decimal TotalMarks { get; set; }
    public int DurationMinutes { get; set; }
    public string CreationMode { get; set; } = "Manual";
    public string? GeneralInstructions { get; set; }
    public QuestionPaperStatus Status { get; set; }
    public int VersionNumber { get; set; }
    public bool IsLocked { get; set; }
    public List<QuestionPaperSectionDto> Sections { get; set; } = new();
    public List<QuestionPaperQuestionDto> Questions { get; set; } = new();
}

public class QuestionPaperSectionDto
{
    public Guid? Id { get; set; }
    public string SectionName { get; set; } = string.Empty;
    public string? Instructions { get; set; }
    public decimal Marks { get; set; }
    public int SortOrder { get; set; }
}

public class QuestionPaperQuestionDto
{
    public Guid Id { get; set; }
    public Guid QuestionPaperSectionId { get; set; }
    public Guid QuestionBankItemId { get; set; }
    public int QuestionNumber { get; set; }
    public decimal Marks { get; set; }
    public int SortOrder { get; set; }
    public string? ChoiceGroupCode { get; set; }
    public string? QuestionText { get; set; }
}

public class CreateUpdateQuestionPaperDto
{
    public Guid AcademicYearId { get; set; }
    public Guid? ExamId { get; set; }
    public Guid? ExamScheduleId { get; set; }
    public Guid StandardId { get; set; }
    public Guid SubjectId { get; set; }
    public string PaperTitle { get; set; } = string.Empty;
    public decimal TotalMarks { get; set; }
    public int DurationMinutes { get; set; }
    public string CreationMode { get; set; } = "Manual";
    public string? GeneralInstructions { get; set; }
    public List<QuestionPaperSectionDto> Sections { get; set; } = new();
}

public class QuestionPaperSearchInput
{
    public Guid? AcademicYearId { get; set; }
    public Guid? ExamId { get; set; }
    public Guid? ExamScheduleId { get; set; }
    public Guid? StandardId { get; set; }
    public Guid? SubjectId { get; set; }
    public QuestionPaperStatus? Status { get; set; }
    public string? Keyword { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class QuestionPaperListResponseDto
{
    public int TotalCount { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public List<QuestionPaperDto> Items { get; set; } = new();
}

public class QuestionPaperBlueprintDto
{
    public Guid? Id { get; set; }
    public Guid QuestionPaperId { get; set; }
    public decimal TotalMarks { get; set; }
    public List<BlueprintChapterRuleDto> ChapterRules { get; set; } = new();
    public List<BlueprintDifficultyRuleDto> DifficultyRules { get; set; } = new();
    public List<BlueprintBloomRuleDto> BloomRules { get; set; } = new();
}

public class BlueprintChapterRuleDto
{
    public Guid ChapterId { get; set; }
    public decimal Marks { get; set; }
    public int? NumberOfQuestions { get; set; }
}

public class BlueprintDifficultyRuleDto
{
    public DifficultyLevel DifficultyLevel { get; set; }
    public decimal Percentage { get; set; }
}

public class BlueprintBloomRuleDto
{
    public BloomLevel BloomLevel { get; set; }
    public decimal Percentage { get; set; }
}

public class BlueprintValidationResultDto
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class AddPaperQuestionDto
{
    public Guid QuestionPaperSectionId { get; set; }
    public Guid QuestionBankItemId { get; set; }
    public int QuestionNumber { get; set; }
    public decimal Marks { get; set; }
    public int SortOrder { get; set; }
    public string? ChoiceGroupCode { get; set; }
}

public class ReorderPaperQuestionsDto
{
    public List<QuestionOrderDto> Items { get; set; } = new();
}

public class QuestionOrderDto
{
    public Guid PaperQuestionId { get; set; }
    public int QuestionNumber { get; set; }
    public int SortOrder { get; set; }
}

public class PaperReviewDto
{
    public string? Comment { get; set; }
}

public class QuestionPaperPreviewDto
{
    public QuestionPaperDto Paper { get; set; } = new();
    public string PrintHtml { get; set; } = string.Empty;
}

public class QuestionPaperDashboardDto
{
    public int TotalPapers { get; set; }
    public int DraftPapers { get; set; }
    public int GeneratedPapers { get; set; }
    public int PendingApproval { get; set; }
    public int ApprovedPapers { get; set; }
    public int PublishedPapers { get; set; }
    public int LockedPapers { get; set; }
}
