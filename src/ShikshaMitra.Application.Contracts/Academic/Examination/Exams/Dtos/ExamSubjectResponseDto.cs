using System;

namespace ShikshaMitra.Academic.Examination.Exams.Dtos;

public class ExamSubjectResponseDto
{
    public Guid ExamSubjectId { get; set; }
    public Guid SubjectId { get; set; }
    public string SubjectName { get; set; }
    public string SubjectColor { get; set; }
    public string Notes { get; set; }
    public DateTime? ExamDate { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public double? TotalMarks { get; set; }
    public double? PassingMarks { get; set; }
}
