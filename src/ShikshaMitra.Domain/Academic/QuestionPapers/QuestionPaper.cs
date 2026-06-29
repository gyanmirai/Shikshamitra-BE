using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using ShikshaMitra.Academic.QuestionBank;

namespace ShikshaMitra.Academic.QuestionPapers;

public class QuestionPaper : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
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
    public Guid? ParentPaperId { get; set; }
    public bool IsLocked { get; set; }
    public ICollection<QuestionPaperSection> Sections { get; private set; }
    public ICollection<QuestionPaperQuestion> Questions { get; private set; }

    private QuestionPaper()
    {
        Sections = new Collection<QuestionPaperSection>();
        Questions = new Collection<QuestionPaperQuestion>();
    }

    public QuestionPaper(Guid id, Guid? tenantId, Guid academicYearId, Guid standardId, Guid subjectId, string paperTitle, decimal totalMarks, int durationMinutes, string creationMode) : this()
    {
        TenantId = tenantId;
        AcademicYearId = academicYearId;
        StandardId = standardId;
        SubjectId = subjectId;
        PaperTitle = Check.NotNullOrWhiteSpace(paperTitle, nameof(paperTitle));
        TotalMarks = totalMarks;
        DurationMinutes = durationMinutes;
        CreationMode = creationMode;
        Status = QuestionPaperStatus.Draft;
        VersionNumber = 1;
    }
}

public class QuestionPaperSection : FullAuditedEntity<Guid>
{
    public Guid QuestionPaperId { get; set; }
    public string SectionName { get; set; } = string.Empty;
    public string? Instructions { get; set; }
    public decimal Marks { get; set; }
    public int SortOrder { get; set; }

    private QuestionPaperSection() { }

    public QuestionPaperSection(Guid id, Guid questionPaperId, string sectionName, decimal marks, int sortOrder, string? instructions = null) : base(id)
    {
        QuestionPaperId = questionPaperId;
        SectionName = Check.NotNullOrWhiteSpace(sectionName, nameof(sectionName));
        Marks = marks;
        SortOrder = sortOrder;
        Instructions = instructions;
    }
}

public class QuestionPaperQuestion : FullAuditedEntity<Guid>
{
    public Guid QuestionPaperId { get; set; }
    public Guid QuestionPaperSectionId { get; set; }
    public Guid QuestionBankItemId { get; set; }
    public int QuestionNumber { get; set; }
    public decimal Marks { get; set; }
    public int SortOrder { get; set; }
    public string? ChoiceGroupCode { get; set; }

    private QuestionPaperQuestion() { }

    public QuestionPaperQuestion(Guid id, Guid questionPaperId, Guid questionPaperSectionId, Guid questionBankItemId, int questionNumber, decimal marks, int sortOrder, string? choiceGroupCode = null) : base(id)
    {
        QuestionPaperId = questionPaperId;
        QuestionPaperSectionId = questionPaperSectionId;
        QuestionBankItemId = questionBankItemId;
        QuestionNumber = questionNumber;
        Marks = marks;
        SortOrder = sortOrder;
        ChoiceGroupCode = choiceGroupCode;
    }
}

public class QuestionPaperBlueprint : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid QuestionPaperId { get; set; }
    public decimal TotalMarks { get; set; }
    public ICollection<QuestionPaperBlueprintChapterRule> ChapterRules { get; private set; }
    public ICollection<QuestionPaperBlueprintDifficultyRule> DifficultyRules { get; private set; }
    public ICollection<QuestionPaperBlueprintBloomRule> BloomRules { get; private set; }

    private QuestionPaperBlueprint()
    {
        ChapterRules = new Collection<QuestionPaperBlueprintChapterRule>();
        DifficultyRules = new Collection<QuestionPaperBlueprintDifficultyRule>();
        BloomRules = new Collection<QuestionPaperBlueprintBloomRule>();
    }

    public QuestionPaperBlueprint(Guid id, Guid? tenantId, Guid questionPaperId, decimal totalMarks) : this()
    {
        TenantId = tenantId;
        QuestionPaperId = questionPaperId;
        TotalMarks = totalMarks;
    }
}

public class QuestionPaperBlueprintChapterRule : FullAuditedEntity<Guid>
{
    public Guid QuestionPaperBlueprintId { get; set; }
    public Guid ChapterId { get; set; }
    public decimal Marks { get; set; }
    public int? NumberOfQuestions { get; set; }

    private QuestionPaperBlueprintChapterRule() { }

    public QuestionPaperBlueprintChapterRule(Guid id, Guid questionPaperBlueprintId, Guid chapterId, decimal marks, int? numberOfQuestions) : base(id)
    {
        QuestionPaperBlueprintId = questionPaperBlueprintId;
        ChapterId = chapterId;
        Marks = marks;
        NumberOfQuestions = numberOfQuestions;
    }
}

public class QuestionPaperBlueprintDifficultyRule : FullAuditedEntity<Guid>
{
    public Guid QuestionPaperBlueprintId { get; set; }
    public DifficultyLevel DifficultyLevel { get; set; }
    public decimal Percentage { get; set; }

    private QuestionPaperBlueprintDifficultyRule() { }

    public QuestionPaperBlueprintDifficultyRule(Guid id, Guid questionPaperBlueprintId, DifficultyLevel difficultyLevel, decimal percentage) : base(id)
    {
        QuestionPaperBlueprintId = questionPaperBlueprintId;
        DifficultyLevel = difficultyLevel;
        Percentage = percentage;
    }
}

public class QuestionPaperBlueprintBloomRule : FullAuditedEntity<Guid>
{
    public Guid QuestionPaperBlueprintId { get; set; }
    public BloomLevel BloomLevel { get; set; }
    public decimal Percentage { get; set; }

    private QuestionPaperBlueprintBloomRule() { }

    public QuestionPaperBlueprintBloomRule(Guid id, Guid questionPaperBlueprintId, BloomLevel bloomLevel, decimal percentage) : base(id)
    {
        QuestionPaperBlueprintId = questionPaperBlueprintId;
        BloomLevel = bloomLevel;
        Percentage = percentage;
    }
}
