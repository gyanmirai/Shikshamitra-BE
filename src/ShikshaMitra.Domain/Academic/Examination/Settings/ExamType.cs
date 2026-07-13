using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Examination.Settings;

public class ExamType : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public Guid AcademicMediumId { get; private set; }
    public string? Category { get; private set; }
    public SettingStatus Status { get; private set; }
    public double? DefaultTotalMarks { get; private set; }
    public double? DefaultWeightage { get; private set; }
    public bool IncludedInResult { get; private set; }
    public bool MarksEntryRequired { get; private set; }
    public bool GradeScaleRequired { get; private set; }
    public bool AllowPracticalOral { get; private set; }
    public Guid? DefaultGradeScaleId { get; private set; }

    private ExamType()
    {
        /* This constructor is for deserialization / ORM purpose */
    }

    public ExamType(Guid id, Guid? tenantId, string name, string? description, Guid academicMediumId,
        string? category, SettingStatus status, double? defaultTotalMarks, double? defaultWeightage,
        bool includedInResult, bool marksEntryRequired, bool gradeScaleRequired, bool allowPracticalOral, Guid? defaultGradeScaleId)
        : base(id)
    {
        TenantId = tenantId;
        SetName(name);
        SetDescription(description);
        AcademicMediumId = academicMediumId;
        SetCategory(category);
        SetStatus(status);
        SetDefaultTotalMarks(defaultTotalMarks);
        SetDefaultWeightage(defaultWeightage);
        SetIncludedInResult(includedInResult);
        SetMarksEntryRequired(marksEntryRequired);
        SetGradeScaleRequired(gradeScaleRequired);
        SetAllowPracticalOral(allowPracticalOral);
        SetDefaultGradeScaleId(defaultGradeScaleId);
    }

    public ExamType(Guid id, Guid? tenantId, string name, string? description, Guid academicMediumId)
        : base(id)
    {
        TenantId = tenantId;
        SetName(name);
        SetDescription(description);
        AcademicMediumId = academicMediumId;
        Status = SettingStatus.Active;
        IncludedInResult = true;
        MarksEntryRequired = true;
        GradeScaleRequired = true;
    }

    public void SetName(string name)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));
        Name = name;
    }

    public void SetDescription(string? description)
    {
        Description = description;
    }

    public void SetCategory(string? category)
    {
        Category = category;
    }

    public void SetStatus(SettingStatus status)
    {
        Status = status;
    }

    public void SetDefaultTotalMarks(double? defaultTotalMarks)
    {
        DefaultTotalMarks = defaultTotalMarks;
    }

    public void SetDefaultWeightage(double? defaultWeightage)
    {
        DefaultWeightage = defaultWeightage;
    }

    public void SetIncludedInResult(bool includedInResult)
    {
        IncludedInResult = includedInResult;
    }

    public void SetMarksEntryRequired(bool marksEntryRequired)
    {
        MarksEntryRequired = marksEntryRequired;
    }

    public void SetGradeScaleRequired(bool gradeScaleRequired)
    {
        GradeScaleRequired = gradeScaleRequired;
    }

    public void SetAllowPracticalOral(bool allowPracticalOral)
    {
        AllowPracticalOral = allowPracticalOral;
    }

    public void SetDefaultGradeScaleId(Guid? defaultGradeScaleId)
    {
        DefaultGradeScaleId = defaultGradeScaleId;
    }
}
