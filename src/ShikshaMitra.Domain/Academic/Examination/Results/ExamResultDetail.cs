using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Examination.Results;

/// <summary>
/// Computed result snapshot for one student in a given exam result header.
/// </summary>
public class ExamResultDetail : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid ResultHeaderId { get; private set; }
    public Guid ExamId { get; private set; }
    public Guid StudentId { get; private set; }
    public Guid DivisionId { get; private set; }

    public double TotalMaxMarks { get; private set; }
    public double TotalObtainedMarks { get; private set; }
    public double Percentage { get; private set; }
    public string Grade { get; private set; } = string.Empty;

    public int? ClassRank { get; private set; }
    public int? SectionRank { get; private set; }

    public ExamStudentResultStatus ResultStatus { get; private set; }

    private ExamResultDetail() { }

    public ExamResultDetail(
        Guid id,
        Guid resultHeaderId,
        Guid examId,
        Guid studentId,
        Guid divisionId,
        double totalMaxMarks,
        double totalObtainedMarks,
        double percentage,
        string grade,
        int? classRank,
        int? sectionRank,
        ExamStudentResultStatus resultStatus,
        Guid? tenantId = null) : base(id)
    {
        ResultHeaderId = resultHeaderId;
        ExamId = examId;
        StudentId = studentId;
        DivisionId = divisionId;
        TotalMaxMarks = totalMaxMarks;
        TotalObtainedMarks = totalObtainedMarks;
        Percentage = percentage;
        Grade = grade;
        ClassRank = classRank;
        SectionRank = sectionRank;
        ResultStatus = resultStatus;
        TenantId = tenantId;
    }

    public void MarkAsWithheld()
    {
        ResultStatus = ExamStudentResultStatus.Withheld;
    }
}
