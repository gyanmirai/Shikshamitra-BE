using System;

namespace ShikshaMitra.Academic.Examination.Quizzes.Dtos;

public class QuizQuestionMarkInputDto
{
    public Guid QuestionId { get; set; }
    public int Marks { get; set; }
    public string? Remarks { get; set; }
}
