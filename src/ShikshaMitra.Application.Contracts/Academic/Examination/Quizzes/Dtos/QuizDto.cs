using System;
using System.Collections.Generic;
using ShikshaMitra.Academic.Examination.Quizzes;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.Examination.Quizzes.Dtos;

public class QuizDto : EntityDto<Guid>
{
    public string QuizTitle { get; set; } = null!;
    public Guid ClassId { get; set; }
    public string ClassName { get; set; } = null!;
    public List<Guid> DivisionIds { get; set; } = new();
    public List<string> DivisionNames { get; set; } = new();
    public Guid SubjectId { get; set; }
    public string SubjectName { get; set; } = null!;
    public List<Guid> ChapterIds { get; set; } = new();
    public List<string> ChapterNames { get; set; } = new();
    public int QuizDuration { get; set; }
    public int TotalMarks { get; set; }
    public int PassingMarks { get; set; }
    public DateTime QuizStartDateTime { get; set; }
    public DateTime QuizEndDateTime { get; set; }
    public QuizTakenType QuizTakenTypeId { get; set; }
    public QuizStatus Status { get; set; }
}
