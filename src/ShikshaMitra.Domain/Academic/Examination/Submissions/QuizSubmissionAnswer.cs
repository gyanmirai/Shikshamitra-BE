using System;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Academic.Examination.Submissions;

public class QuizSubmissionAnswer : Entity<Guid>
{
    public Guid QuizSubmissionId { get; private set; }
    public Guid QuestionId { get; private set; }
    public string Answer { get; private set; } = null!;
    public int Marks { get; private set; }
    public string? Remarks { get; private set; }

    private QuizSubmissionAnswer() { }

    public QuizSubmissionAnswer(
        Guid id,
        Guid quizSubmissionId,
        Guid questionId,
        string answer) : base(id)
    {
        QuizSubmissionId = quizSubmissionId;
        QuestionId = questionId;
        Answer = answer;
        Marks = 0; // Initial marks
    }

    internal void SetSubmissionId(Guid quizSubmissionId)
    {
        QuizSubmissionId = quizSubmissionId;
    }

    public void SetMarks(int marks, string? remarks)
    {
        Marks = marks;
        Remarks = remarks;
    }
}
