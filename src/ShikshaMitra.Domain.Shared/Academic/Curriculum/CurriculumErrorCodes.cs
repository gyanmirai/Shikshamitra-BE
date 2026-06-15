namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// Centralized error code catalog for Curriculum module BusinessExceptions.
/// Convention: "Academic:Curriculum:{6-digit code}"
/// Ranges: Subject 01xxxx
/// </summary>
public static class CurriculumErrorCodes
{
    // ── Subject: 01xxxx ──────────────────────────────────────────

    public const string SubjectEducationMediumNotFound = "Academic:Curriculum:010001";
    public const string DuplicateSubjectName = "Academic:Curriculum:010002";
    public const string DuplicateSubjectColor = "Academic:Curriculum:010003";
    public const string SubjectNotFound = "Academic:Curriculum:010004";
    public const string SubjectIsLastInElectiveGroup = "Academic:Curriculum:010005";

    // ── Subject Assignment: 02xxxx ───────────────────────────────

    public const string DuplicateSubjectAssignment = "Academic:Curriculum:020001";

    // ── Elective Group: 03xxxx ───────────────────────────────────

    public const string ElectiveGroupHasActiveAllocations = "Academic:Curriculum:030001";
}
