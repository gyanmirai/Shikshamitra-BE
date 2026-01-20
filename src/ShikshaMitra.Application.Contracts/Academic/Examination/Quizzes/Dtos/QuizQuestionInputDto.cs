using System.Collections.Generic;
using ShikshaMitra.Academic.Examination.Quizzes;

namespace ShikshaMitra.Academic.Examination.Quizzes.Dtos;

public class QuizQuestionInputDto
{
    public string Question { get; set; } = null!;
    public QuizQuestionType QuestionType { get; set; }
    public List<string>? Options { get; set; }
    public bool IsRequired { get; set; }
    public string? CorrectAnswer { get; set; }
    public int Marks { get; set; }
    public string? QuestionTime { get; set; } // Could be time string or timespan
}
