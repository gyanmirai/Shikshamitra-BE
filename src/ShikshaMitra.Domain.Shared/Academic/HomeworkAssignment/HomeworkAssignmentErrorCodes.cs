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
    public const string AssignmentTypeIsInUse = "Academic:HomeworkAssignment:010003";

    // ── HomeworkType: 02xxxx ──────────────────────────────────────

    public const string DuplicateHomeworkType = "Academic:HomeworkAssignment:020001";
    public const string HomeworkTypeNotFound = "Academic:HomeworkAssignment:020002";
    public const string HomeworkTypeIsInUse = "Academic:HomeworkAssignment:020003";
    public const string AtLeastOneActiveHomeworkTypeMustRemain = "Academic:HomeworkAssignment:020004";

    // ── Shared: 09xxxx ────────────────────────────────────────────

    public const string AcademicMediumNotFound = "Academic:HomeworkAssignment:090001";
    public const string HomeworkIsInUse = "Academic:HomeworkAssignment:090002";
    public const string AssignmentIsInUse = "Academic:HomeworkAssignment:090003";
    public const string AtLeastOneActiveAssignmentTypeMustRemain = "Academic:HomeworkAssignment:010004";
}
