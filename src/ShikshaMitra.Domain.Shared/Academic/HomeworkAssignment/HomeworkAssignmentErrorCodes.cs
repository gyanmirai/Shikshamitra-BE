namespace ShikshaMitra.Academic.HomeworkAssignment;

/// <summary>
/// Centralized error code catalog for Homework and Assignment module BusinessExceptions.
/// Convention: "Academic:HomeworkAssignment:{6-digit code}"
/// Ranges: AssignmentType 01xxxx, HomeworkType 02xxxx, Shared 09xxxx
/// </summary>
public static class HomeworkAssignmentErrorCodes
{
    // ── AssignmentType: 01xxxx ────────────────────────────────────

    public const string DuplicateAssignmentType = "Academic:HomeworkAssignment:010001";
    public const string AssignmentTypeNotFound = "Academic:HomeworkAssignment:010002";

    // ── HomeworkType: 02xxxx ──────────────────────────────────────

    public const string DuplicateHomeworkType = "Academic:HomeworkAssignment:020001";
    public const string HomeworkTypeNotFound = "Academic:HomeworkAssignment:020002";

    // ── Shared: 09xxxx ────────────────────────────────────────────

    public const string AcademicMediumNotFound = "Academic:HomeworkAssignment:090001";
}
