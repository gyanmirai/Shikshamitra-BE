using System;

namespace ShikshaMitra.Academic.Examination.Exams.Dtos;

public class QuestionPaperExamLookupInput
{
    /// <summary>
    /// The academic year selected by the question-paper wizard. Exams are
    /// currently scoped by the standard's education medium rather than by
    /// academic year, so the application service resolves the medium from
    /// StandardId instead of comparing this value to a medium id.
    /// </summary>
    public Guid AcademicYearId { get; set; }

    /// <summary>
    /// Kept for backwards compatibility with older clients. New clients
    /// should send AcademicYearId.
    /// </summary>
    [Obsolete("Use AcademicYearId. The medium is resolved from StandardId.")]
    public Guid? AcademicMediumId { get; set; }

    public Guid StandardId { get; set; }
    public Guid SubjectId { get; set; }
}

public class QuestionPaperExamLookupDto
{
    public Guid ExamId { get; set; }
    public Guid ExamSubjectId { get; set; }
    public string ExamName { get; set; } = string.Empty;
    public Guid StandardId { get; set; }
    public Guid SubjectId { get; set; }
    public string ExamTypeName { get; set; } = string.Empty;
    public DateTime ExamDate { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public double? TotalMarks { get; set; }
    public double? PassingMarks { get; set; }
    public ExamStatus Status { get; set; }
}
