namespace ShikshaMitra.Students.StudentEnrollments;

/// <summary>
/// Enums for the Student Enrollment sub-domain not already in Domain.Shared root.
/// </summary>

/// <summary>
/// Represents the result of an entrance exam for a student.
/// </summary>
public enum EntranceExamResult
{
    Pending = 0,
    Pass    = 1,
    Fail    = 2,
}

/// <summary>
/// Identifies the actor who performed an enrollment lifecycle transition.
/// </summary>
public enum EnrollmentTransitionActor
{
    System    = 0,
    Staff     = 1,
    Principal = 2,
}
