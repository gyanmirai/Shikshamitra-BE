namespace ShikshaMitra.Students;

/// <summary>
/// Centralised error-code catalog for all Student module BusinessExceptions.
/// Convention: "ST:{SubModule}:{6-digit code}"
/// Ranges:
///   Student     01xxxx  (documents 011xxx)
///   Attendance  02xxxx
///   Enrollment  03xxxx
///   Fees        04xxxx
///   Records     05xxxx
/// </summary>
public static class StudentErrorCodes
{
    // ── Core Student: 01xxxx ─────────────────────────────────────

    public const string StudentNotFound                 = "ST:Student:010001";
    public const string StudentFirstNameRequired        = "ST:Student:010003";
    public const string StudentLastNameRequired         = "ST:Student:010004";
    public const string StudentDobInFuture              = "ST:Student:010005";
    public const string StudentInvalidGender            = "ST:Student:010006";
    public const string StudentInvalidCasteCategory     = "ST:Student:010007";
    public const string StudentMobileRequired           = "ST:Student:010008";
    public const string StudentEmailInvalid             = "ST:Student:010009";
    public const string StudentAcademicMediumRequired   = "ST:Student:010010";
    public const string StudentInvalidStatusTransition  = "ST:Student:010011";

    // Student — Documents: 011xxx
    public const string StudentDocumentNotFound         = "ST:Student:010101";
    public const string StudentDocumentTypeNotFound     = "ST:Student:010102";
    public const string StudentDocumentFileTooLarge     = "ST:Student:010103";
    public const string StudentDocumentInvalidExtension = "ST:Student:010104";

    // ── Attendance & Leave: 02xxxx ────────────────────────────────

    public const string AttendanceDayAlreadyLocked          = "ST:Attendance:020001";
    public const string CannotModifyLockedAttendance         = "ST:Attendance:020002";
    public const string AttendanceDayNotFound                = "ST:Attendance:020003";
    public const string AttendanceEntryNotFound              = "ST:Attendance:020004";
    public const string AttendanceDivisionRequired           = "ST:Attendance:020005";
    public const string AttendanceAcademicYearRequired       = "ST:Attendance:020006";

    // Leave
    public const string LeaveNotFound                        = "ST:Attendance:020101";
    public const string CannotUpdateNonPendingLeave          = "ST:Attendance:020102";
    public const string CannotApproveNonPendingLeave         = "ST:Attendance:020103";
    public const string CannotRejectNonPendingLeave          = "ST:Attendance:020104";
    public const string LeaveDescriptionTooLong              = "ST:Attendance:020105";
    public const string LeaveRemarksTooLong                  = "ST:Attendance:020106";
    public const string LeaveRejectionReasonTooLong          = "ST:Attendance:020107";
    public const string LeaveAttachmentTooLarge              = "ST:Attendance:020108";
    public const string LeaveAttachmentFileNameTooLong       = "ST:Attendance:020109";
    public const string LeaveEndDateRequired                 = "ST:Attendance:020110";
    public const string LeaveEndDateBeforeStartDate          = "ST:Attendance:020111";

    // ── Student Enrollments: 03xxxx ──────────────────────────────

    public const string InvalidLifecycleTransition           = "ST:Enrollment:030001";
    public const string AdmissionAlreadyApproved             = "ST:Enrollment:030002";
    public const string AdmissionAlreadyRejected             = "ST:Enrollment:030003";
    public const string AdmissionRemarksRequired             = "ST:Enrollment:030004";
    public const string EnrollmentNotFound                   = "ST:Enrollment:030005";
    public const string EntranceExamNotPassed                = "ST:Enrollment:030006";
    public const string RegistrationFormAlreadyIssued        = "ST:Enrollment:030007";
    public const string EntranceExamSlotNotFound             = "ST:Enrollment:030008";
    public const string InquiryNoRequired                    = "ST:Enrollment:030009";

    // ── Student Fees: 04xxxx ─────────────────────────────────────

    public const string FeeRecordNotFound                    = "ST:Fees:040001";
    public const string FeeRecordAlreadyLocked               = "ST:Fees:040002";
    public const string FeeStructureNotFound                 = "ST:Fees:040003";
    public const string FeeAcademicYearRequired              = "ST:Fees:040004";

    // ── Student Records: 05xxxx ──────────────────────────────────

    public const string ReportExportFailed                   = "ST:Records:050001";
    public const string HouseSettingsNotFound                = "ST:Records:050002";
    public const string CertificateTypeNotFound              = "ST:Records:050003";
    public const string HouseSettingsAlreadyExists           = "ST:Records:050004";
    public const string CertificateNotFound                  = "ST:Records:050008";
    public const string CertificateTemplateNotFound          = "ST:Records:050009";
    public const string CertificateNumberGenerationConflict  = "ST:Records:050010";
}
