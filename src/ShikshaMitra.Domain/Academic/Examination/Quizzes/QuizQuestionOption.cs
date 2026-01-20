using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Academic.Examination.Quizzes;

public class QuizQuestionOption : Entity<Guid>
{
    public Guid QuizQuestionId { get; private set; }
    public string OptionText { get; private set; } = null!;

    private QuizQuestionOption() { }

    public QuizQuestionOption(Guid id, Guid quizQuestionId, string optionText) : base(id)
    {
        QuizQuestionId = quizQuestionId;
        OptionText = Check.NotNullOrWhiteSpace(optionText, nameof(optionText));
    }

    internal void SetQuestionId(Guid quizQuestionId)
    {
        QuizQuestionId = quizQuestionId;
    }
}
