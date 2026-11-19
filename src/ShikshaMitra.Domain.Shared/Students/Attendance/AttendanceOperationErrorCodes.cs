namespace ShikshaMitra.Students.Attendance;

public static class AttendanceOperationErrorCodes
{
	public const string AcademicYearOutsideTenant = "StudentAttendance:AcademicYearOutsideTenant";
	public const string CannotUnmarkSubmittedAttendance = "StudentAttendance:CannotUnmarkSubmittedAttendance";
	public const string ConcurrencyConflict = "StudentAttendance:ConcurrencyConflict";
	public const string CorrectionAlreadyPending = "StudentAttendance:CorrectionAlreadyPending";
	public const string CorrectionAlreadyReviewed = "StudentAttendance:CorrectionAlreadyReviewed";
	public const string CorrectionApprovalRequired = "StudentAttendance:CorrectionApprovalRequired";
	public const string CorrectionHasNoChange = "StudentAttendance:CorrectionHasNoChange";
	public const string CorrectionReasonRequired = "StudentAttendance:CorrectionReasonRequired";
	public const string CreditCorrectionRequired = "StudentAttendance:CreditCorrectionRequired";
	public const string DateOutsideAcademicYear = "StudentAttendance:DateOutsideAcademicYear";
	public const string DetailDisabledByPolicy = "StudentAttendance:DetailDisabledByPolicy";
	public const string DetailsRequirePresent = "StudentAttendance:DetailsRequirePresent";
	public const string DuplicateOrEmptyEntries = "StudentAttendance:DuplicateOrEmptyEntries";
	public const string IdempotencyKeyReused = "StudentAttendance:IdempotencyKeyReused";
	public const string IncompleteRoster = "StudentAttendance:IncompleteRoster";
	public const string InvalidCredit = "StudentAttendance:InvalidCredit";
	public const string InvalidDateRange = "StudentAttendance:InvalidDateRange";
	public const string InvalidStatus = "StudentAttendance:InvalidStatus";
	public const string LeaveObservationConflictRequiresReason = "StudentAttendance:LeaveObservationConflictRequiresReason";
	public const string NoRecordedAttendanceToCorrect = "StudentAttendance:NoRecordedAttendanceToCorrect";
	public const string OverlappingApprovedLeave = "StudentAttendance:OverlappingApprovedLeave";
	public const string PlacementRequired = "StudentAttendance:PlacementRequired";
	public const string RosterRevisionRequired = "StudentAttendance:RosterRevisionRequired";
	public const string SchoolNotExpected = "StudentAttendance:SchoolNotExpected";
	public const string SelfApproval = "StudentAttendance:SelfApproval";
	public const string StaleCorrection = "StudentAttendance:StaleCorrection";
	public const string StaleRoster = "StudentAttendance:StaleRoster";
	public const string StartDateAlreadyConfirmed = "StudentAttendance:StartDateAlreadyConfirmed";
	public const string StudentOutsideDatedRoster = "StudentAttendance:StudentOutsideDatedRoster";
}
