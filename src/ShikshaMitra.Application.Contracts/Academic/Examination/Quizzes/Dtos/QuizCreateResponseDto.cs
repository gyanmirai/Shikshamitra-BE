using System;
using ShikshaMitra.Academic.Examination.Quizzes;

namespace ShikshaMitra.Academic.Examination.Quizzes.Dtos;

public class QuizCreateResponseDto
{
    public Guid QuizId { get; set; }
    public QuizStatus Status { get; set; }
}
