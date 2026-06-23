namespace ShikshaMitra.Academic.Examination.Exams;

/// <summary>
/// Centralised error-code catalog for all Examination module BusinessExceptions.
/// Convention: "AC:Exam:{6-digit code}"
/// Ranges:
///   Scheduling  050xxx
/// </summary>
public static class ExamErrorCodes
{
    /// <summary>
    /// A subject in the incoming schedule overlaps in time with an existing exam
    /// for the same class and division on the same date.
    /// </summary>
    public const string ScheduleOverlap = "AC:Exam:050001";

    /// <summary>
    /// The same subject is already scheduled for the same class, division, and
    /// overlapping time slot in another exam.
    /// </summary>
    public const string DuplicateSubjectSchedule = "AC:Exam:050002";

    /// <summary>
    /// The exam type is currently in use.
    /// </summary>
    public const string ExamTypeIsInUse = "AC:Exam:050003";

    /// <summary>
    /// The grading scale is currently in use.
    /// </summary>
    public const string GradingScaleIsInUse = "AC:Exam:050004";

    /// <summary>
    /// The exam is currently in use (e.g. marks entered, seat allocations made).
    /// </summary>
    public const string ExamIsInUse = "AC:Exam:050005";
}
