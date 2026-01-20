using System;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Academic.Examination.Quizzes;

public class QuizChapter : Entity<Guid>
{
    public Guid QuizId { get; private set; }
    public Guid ChapterId { get; private set; }

    private QuizChapter() { }

    public QuizChapter(Guid id, Guid quizId, Guid chapterId) : base(id)
    {
        QuizId = quizId;
        ChapterId = chapterId;
    }

    internal void SetQuizId(Guid quizId)
    {
        QuizId = quizId;
    }
}
