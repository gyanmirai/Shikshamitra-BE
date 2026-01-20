using System;
using System.Collections.Generic;
using ShikshaMitra.Academic.Examination.Quizzes;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.Examination.Quizzes.Dtos;

public class QuizListDto : EntityDto<Guid>
{
    public string QuizTitle { get; set; } = null!;
    public string SubjectName { get; set; } = null!;
    public string ClassName { get; set; } = null!;
    public List<string> Divisions { get; set; } = new();
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public int Duration { get; set; }
    public int TotalMarks { get; set; }
    public int PassingMarks { get; set; }
    public QuizStatus Status { get; set; }
    public string? SubjectColor { get; set; }
    public string? SubjectCode { get; set; }
}
