using System;
using ShikshaMitra.Academic.Examination.Quizzes;

namespace ShikshaMitra.Academic.Examination.Quizzes.Dtos;

public class QuizStudentResultItemDto
{
    public string GrNo { get; set; } = null!;
    public string StudentName { get; set; } = null!;
    public int TotalMarks { get; set; }
    public int PassingMarks { get; set; }
    public int? ScoringMarks { get; set; }
    public StudentResultStatus Status { get; set; }
    public string RollNo { get; set; } = null!;
    public Guid StudentId { get; set; }
}
