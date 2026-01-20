using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Academic.Examination.Quizzes;

public class QuizQuestion : Entity<Guid>
{
    public Guid QuizId { get; private set; }
    public string QuestionText { get; private set; } = null!;
    public QuizQuestionType QuestionType { get; private set; }
    public int Marks { get; private set; }
    public string? QuestionTime { get; private set; }
    public bool IsRequired { get; private set; }
    public string? CorrectAnswer { get; private set; }

    public ICollection<QuizQuestionOption> Options { get; private set; }

    private QuizQuestion()
    {
        Options = new Collection<QuizQuestionOption>();
    }

    public QuizQuestion(
        Guid id,
        Guid quizId,
        string questionText,
        QuizQuestionType questionType,
        int marks,
        string? questionTime,
        bool isRequired,
        string? correctAnswer) : base(id)
    {
        QuizId = quizId;
        QuestionText = Check.NotNullOrWhiteSpace(questionText, nameof(questionText));
        QuestionType = questionType;
        Marks = marks;
        QuestionTime = questionTime;
        IsRequired = isRequired;
        CorrectAnswer = correctAnswer;
        
        Options = new Collection<QuizQuestionOption>();
    }

    internal void SetQuizId(Guid quizId)
    {
        QuizId = quizId;
    }

    public void AddOption(Guid id, string optionText)
    {
        Options.Add(new QuizQuestionOption(id, Id, optionText));
    }
}
