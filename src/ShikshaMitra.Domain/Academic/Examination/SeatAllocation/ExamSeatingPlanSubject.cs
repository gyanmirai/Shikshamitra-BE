using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Examination.SeatAllocation;

public class ExamSeatingPlanSubject : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid SeatingPlanId { get; private set; }
    public Guid ExamId { get; private set; }
    public Guid ExamSubjectId { get; private set; }
    public Guid SubjectId { get; private set; }

    private ExamSeatingPlanSubject() { }

    public ExamSeatingPlanSubject(
        Guid id,
        Guid? tenantId,
        Guid seatingPlanId,
        Guid examId,
        Guid examSubjectId,
        Guid subjectId) : base(id)
    {
        TenantId = tenantId;
        SeatingPlanId = seatingPlanId;
        ExamId = examId;
        ExamSubjectId = examSubjectId;
        SubjectId = subjectId;
    }
}
