using System;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Academic.Examination.Exams;

public class ExamDivision : Entity<Guid>
{
    public Guid ExamId { get; private set; }
    public Guid DivisionId { get; private set; }

    private ExamDivision() { }

    public ExamDivision(Guid id, Guid examId, Guid divisionId) : base(id)
    {
        ExamId = examId;
        DivisionId = divisionId;
    }

    internal void SetExamId(Guid examId)
    {
        ExamId = examId;
    }
}
