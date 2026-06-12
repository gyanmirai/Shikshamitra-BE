using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Examination.Exams;

/// <summary>
/// Tracks the marks workflow state for a specific (Exam, Subject, Division) combination.
/// </summary>
public class ExamMarksWorkflowState : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid ExamId { get; private set; }
    public Guid SubjectId { get; private set; }
    public Guid DivisionId { get; private set; }
    public ExamMarksWorkflowStatus Status { get; private set; }

    private ExamMarksWorkflowState() { }

    public ExamMarksWorkflowState(
        Guid id,
        Guid examId,
        Guid subjectId,
        Guid divisionId,
        Guid? tenantId = null) : base(id)
    {
        ExamId = examId;
        SubjectId = subjectId;
        DivisionId = divisionId;
        Status = ExamMarksWorkflowStatus.Draft;
        TenantId = tenantId;
    }

    public void TransitionTo(ExamMarksWorkflowStatus newStatus)
    {
        Status = newStatus;
    }
}
