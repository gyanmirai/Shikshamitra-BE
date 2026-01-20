using System;
using System.Collections.Generic;
using ShikshaMitra.Academic.Examination.Quizzes;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.Examination.Quizzes.Dtos;

public class QuizQuestionResponseDto : EntityDto<Guid>
{
    public string Question { get; set; } = null!;
    public QuizQuestionType QuestionType { get; set; }
    public List<string> Options { get; set; } = new();
    public bool IsRequired { get; set; }
    public string? CorrectAnswer { get; set; }
    public int Marks { get; set; }
    public string? QuestionTime { get; set; }
}
