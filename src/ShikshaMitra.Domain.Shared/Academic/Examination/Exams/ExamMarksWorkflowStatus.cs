namespace ShikshaMitra.Academic.Examination.Exams;

public enum ExamMarksWorkflowStatus
{
    Draft = 0,
    Submitted = 1,
    Verified = 2,
    Published = 3,   // Published also means locked (final state)
    ChangesRequested = 4,
    UnderVerification = 5
}
