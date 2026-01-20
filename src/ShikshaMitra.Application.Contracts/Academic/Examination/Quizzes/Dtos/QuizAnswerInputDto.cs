using System;

namespace ShikshaMitra.Academic.Examination.Quizzes.Dtos;

public class QuizAnswerInputDto
{
    public Guid QuestionId { get; set; }
    public string Answer { get; set; } = null!;
}
