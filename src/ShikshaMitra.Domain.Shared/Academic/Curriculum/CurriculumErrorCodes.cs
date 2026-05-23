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
}
