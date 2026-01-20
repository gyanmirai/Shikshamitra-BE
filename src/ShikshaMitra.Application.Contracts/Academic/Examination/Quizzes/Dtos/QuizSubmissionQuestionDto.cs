using System;
using System.Collections.Generic;
using ShikshaMitra.Academic.Examination.Quizzes;

namespace ShikshaMitra.Academic.Examination.Quizzes.Dtos;

public class QuizSubmissionQuestionDto
{
    public Guid Id { get; set; }
    public QuizQuestionType Type { get; set; }
    public string Question { get; set; } = null!;
    public List<string>? Options { get; set; }
    public string? CorrectAnswer { get; set; }
    public string? UserAnswer { get; set; }
    public int? Marks { get; set; }
    public int TotalMarks { get; set; }
    public string? Remarks { get; set; }
}
