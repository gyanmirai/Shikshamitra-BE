using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.QuestionBank;

public class QuestionBankItem : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid AcademicYearId { get; set; }
    public Guid? BoardId { get; set; }
    public Guid? MediumId { get; set; }
    public Guid StandardId { get; set; }
    public Guid SubjectId { get; set; }
    public Guid? ChapterId { get; set; }
    public Guid? TopicId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string? PlainTextSearchContent { get; set; }
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
    public ICollection<QuestionOption> Options { get; private set; }
    public ICollection<QuestionAttachment> Attachments { get; private set; }
    public ICollection<QuestionTag> Tags { get; private set; }

    private QuestionBankItem()
    {
        Options = new Collection<QuestionOption>();
        Attachments = new Collection<QuestionAttachment>();
        Tags = new Collection<QuestionTag>();
    }

    public QuestionBankItem(Guid id, Guid? tenantId, Guid academicYearId, Guid standardId, Guid subjectId, string questionText, QuestionType questionType, DifficultyLevel difficultyLevel, decimal marks, Guid createdByTeacherId, QuestionVisibility visibility) : this()
    {
        TenantId = tenantId;
        AcademicYearId = academicYearId;
        StandardId = standardId;
        SubjectId = subjectId;
        QuestionText = Check.NotNullOrWhiteSpace(questionText, nameof(questionText));
        PlainTextSearchContent = Normalize(questionText);
        QuestionType = questionType;
        DifficultyLevel = difficultyLevel;
        Marks = marks;
        CreatedByTeacherId = createdByTeacherId;
        Visibility = visibility;
        Status = QuestionStatus.Draft;
        VersionNumber = 1;
    }

    public void SetReview(QuestionStatus status, Guid reviewerId, string? comment)
    {
        Status = status;
        ReviewedByUserId = reviewerId;
        ReviewedAt = DateTime.UtcNow;
        ReviewComment = comment;
    }

    public static string Normalize(string text) => text.Trim().ToLowerInvariant();
}

public class QuestionOption : FullAuditedEntity<Guid>
{
    public Guid QuestionBankItemId { get; set; }
    public string OptionText { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public int SortOrder { get; set; }

    private QuestionOption() { }

    public QuestionOption(Guid id, Guid questionBankItemId, string optionText, bool isCorrect, int sortOrder) : base(id)
    {
        QuestionBankItemId = questionBankItemId;
        OptionText = optionText;
        IsCorrect = isCorrect;
        SortOrder = sortOrder;
    }
}

public class QuestionAttachment : FullAuditedEntity<Guid>
{
    public Guid QuestionBankItemId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public QuestionAttachmentType AttachmentType { get; set; }
}

public class QuestionTag : FullAuditedEntity<Guid>
{
    public Guid QuestionBankItemId { get; set; }
    public string TagName { get; set; } = string.Empty;

    private QuestionTag() { }

    public QuestionTag(Guid id, Guid questionBankItemId, string tagName) : base(id)
    {
        QuestionBankItemId = questionBankItemId;
        TagName = tagName.Trim();
    }
}

public class QuestionUsageLog : FullAuditedEntity<Guid>
{
    public Guid QuestionBankItemId { get; set; }
    public QuestionUsageSource Source { get; set; }
    public Guid SourceEntityId { get; set; }
    public DateTime UsedAt { get; set; }
    public Guid UsedByUserId { get; set; }

    private QuestionUsageLog() { }

    public QuestionUsageLog(Guid id, Guid questionBankItemId, QuestionUsageSource source, Guid sourceEntityId, DateTime usedAt, Guid usedByUserId) : base(id)
    {
        QuestionBankItemId = questionBankItemId;
        Source = source;
        SourceEntityId = sourceEntityId;
        UsedAt = usedAt;
        UsedByUserId = usedByUserId;
    }
}
