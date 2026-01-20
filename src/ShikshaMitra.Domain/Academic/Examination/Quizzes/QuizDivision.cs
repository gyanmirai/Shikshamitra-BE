using System;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Academic.Examination.Quizzes;

public class QuizDivision : Entity<Guid>
{
    public Guid QuizId { get; private set; }
    public Guid DivisionId { get; private set; }

    private QuizDivision() { }

    public QuizDivision(Guid id, Guid quizId, Guid divisionId) : base(id)
    {
        QuizId = quizId;
        DivisionId = divisionId;
    }

    internal void SetQuizId(Guid quizId)
    {
        QuizId = quizId;
    }
}
