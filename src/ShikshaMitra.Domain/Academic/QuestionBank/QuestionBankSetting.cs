using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.QuestionBank;

public class QuestionBankSetting : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid AcademicYearId { get; set; }
    public QuestionApprovalMode ApprovalMode { get; set; }
    public QuestionVisibility DefaultVisibility { get; set; }
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

    private QuestionBankSetting() { }

    public QuestionBankSetting(Guid id, Guid? tenantId, Guid academicYearId) : base(id)
    {
        TenantId = tenantId;
        AcademicYearId = academicYearId;
        ApprovalMode = QuestionApprovalMode.HodApproval;
        DefaultVisibility = QuestionVisibility.Department;
    }
}
