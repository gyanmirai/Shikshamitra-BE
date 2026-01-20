using System;
using System.Collections.Generic;
using ShikshaMitra.Academic.Examination.Quizzes;

namespace ShikshaMitra.Academic.Examination.Quizzes.Dtos;

public class QuizStep1RequestDto
{
    public string QuizTitle { get; set; } = null!;
    public Guid ClassId { get; set; }
    public List<Guid> DivisionIds { get; set; } = new();
    public Guid SubjectId { get; set; }
    public List<Guid>? ChapterIds { get; set; }
    public int QuizDuration { get; set; }
    public int TotalMarks { get; set; }
    public int PassingMarks { get; set; }
    public DateTime QuizStartDateTime { get; set; }
    public DateTime QuizEndDateTime { get; set; }
    public QuizTakenType QuizTakenTypeId { get; set; }
}
