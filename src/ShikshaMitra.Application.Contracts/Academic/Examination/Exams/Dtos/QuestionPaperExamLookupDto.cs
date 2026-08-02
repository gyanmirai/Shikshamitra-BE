using System;

namespace ShikshaMitra.Academic.Examination.Exams.Dtos;

public class QuestionPaperExamLookupInput
{
    public Guid AcademicMediumId { get; set; }
    public Guid StandardId { get; set; }
    public Guid SubjectId { get; set; }
}

public class QuestionPaperExamLookupDto
{
    public Guid ExamId { get; set; }
    public Guid ExamSubjectId { get; set; }
    public string ExamName { get; set; } = string.Empty;
    public string ExamTypeName { get; set; } = string.Empty;
    public DateTime ExamDate { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public double? TotalMarks { get; set; }
    public double? PassingMarks { get; set; }
    public ExamStatus Status { get; set; }
}
