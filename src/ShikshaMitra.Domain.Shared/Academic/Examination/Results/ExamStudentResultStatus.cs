namespace ShikshaMitra.Academic.Examination.Results;

public enum ExamStudentResultStatus
{
    Pass = 0,
    Fail = 1,
    Absent = 2,
    Compartment = 3,  // Passed overall but failed one or more subjects
    Withheld = 4
}
