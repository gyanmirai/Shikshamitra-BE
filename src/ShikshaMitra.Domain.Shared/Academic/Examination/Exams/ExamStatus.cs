namespace ShikshaMitra.Academic.Examination.Exams;

public enum ExamStatus
{
    Pending = 0,
    Published = 1,
    Completed = 2,
    /// <summary>Computed schedule view status; it is not persisted as a lifecycle transition.</summary>
    Ongoing = 3
}
