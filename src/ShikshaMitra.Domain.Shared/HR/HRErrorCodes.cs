namespace ShikshaMitra.HR;

/// <summary>
/// Centralized error code catalog for all HR module BusinessExceptions.
/// Convention: "HR:{SubModule}:{6-digit code}"
/// Ranges: Employee 01xxxx, Leave 02xxxx, Payroll 03xxxx, Calendar 04xxxx, Reports 05xxxx
/// </summary>
public static class HRErrorCodes
{
	// ── Employee: 01xxxx ──────────────────────────────────────────

	public const string EmployeeEmailRequired = "HR:Employee:010001";
	public const string EmployeeMobileRequired = "HR:Employee:010002";
	public const string EmployeeMobileTooShort = "HR:Employee:010003";
	public const string EmployeeEmailAlreadyExists = "HR:Employee:010004";
	public const string EmployeeUsernameGenerationFailed = "HR:Employee:010005";
	public const string EmployeeUserCreationFailed = "HR:Employee:010006";
	public const string EmployeeUserVerificationFailed = "HR:Employee:010007";
	public const string EmployeeNotFound = "HR:Employee:010008";
	public const string InvalidGenderValue = "HR:Employee:010009";
	public const string InvalidCasteTypeValue = "HR:Employee:010010";
	public const string InvalidMaritalStatusValue = "HR:Employee:010011";
	public const string InvalidBloodGroupValue = "HR:Employee:010012";
	public const string UserNotFound = "HR:Employee:010013";
	public const string RoleNotFound = "HR:Employee:010014";
	public const string RoleNameMissing = "HR:Employee:010015";
	public const string RoleAssignmentFailed = "HR:Employee:010016";
	public const string RoleRemovalFailed = "HR:Employee:010017";
	public const string DobInFuture = "HR:Employee:010018";
	public const string DojBeforeDob = "HR:Employee:010019";
	public const string FamilyMemberNameRequired = "HR:Employee:010020";
	public const string FamilyMemberNameTooLong = "HR:Employee:010021";
	public const string FamilyContactTooLong = "HR:Employee:010022";
	public const string FamilyPincodeTooLong = "HR:Employee:010023";

	// Employee — Documents
	public const string EmployeeDocumentNotFound = "HR:Employee:010101";
	public const string EmployeeDocumentConfigurationNotFound = "HR:Employee:010102";

	// Employee — Status
	public const string EmployeeStatusRemarksRequired = "HR:Employee:010024";
	public const string EmployeeStatusInvalidTransition = "HR:Employee:010025";

	// Employee — Payroll
	public const string EmployeePayrollStructureRequired = "HR:Employee:010201";
	public const string EmployeePayrollDetailsRequired = "HR:Employee:010202";
	public const string EmployeePayrollInvalidEarningComponent = "HR:Employee:010203";
	public const string EmployeePayrollInvalidDeductionComponent = "HR:Employee:010204";
	public const string EmployeePayrollDuplicateEarningComponent = "HR:Employee:010205";
	public const string EmployeePayrollDuplicateDeductionComponent = "HR:Employee:010206";
	public const string EmployeePayrollComponentInBothLists = "HR:Employee:010207";
	public const string EmployeePayrollNegativeSalary = "HR:Employee:010208";
	public const string EmployeePayrollInvalidTdsPercentage = "HR:Employee:010209";
	public const string EmployeePayrollNegativeTdsAmount = "HR:Employee:010210";
	public const string EmployeePayrollInvalidIfscCode = "HR:Employee:010211";
	public const string EmployeePayrollInvalidAccountNumber = "HR:Employee:010212";
	public const string EmployeePayrollBankNameRequired = "HR:Employee:010213";
	public const string EmployeePayrollAccountHolderRequired = "HR:Employee:010214";
	public const string EmployeePayrollComponentAmountInvalid = "HR:Employee:010215";
	public const string EmployeePayrollEmptyComponentId = "HR:Employee:010216";

	// ── Attendance & Leave: 02xxxx ────────────────────────────────

	public const string LeaveInvalidMonth = "HR:Leave:020001";
	public const string LeaveInvalidYear = "HR:Leave:020002";
	public const string LeaveInvalidPage = "HR:Leave:020003";
	public const string LeaveInvalidPageSize = "HR:Leave:020004";
	public const string LeaveUserNotAuthenticated = "HR:Leave:020005";
	public const string LeaveEmployeeNotFound = "HR:Leave:020006";
	public const string LeaveInvalidStatus = "HR:Leave:020007";
	public const string LeaveInvalidStatusTransition = "HR:Leave:020008";
	public const string LeaveCancelApproved = "HR:Leave:020009";
	public const string LeaveCancelRejected = "HR:Leave:020010";
	public const string LeaveDeleteOwnOnly = "HR:Leave:020011";
	public const string LeaveStartAfterEnd = "HR:Leave:020012";
	public const string LeavePastDate = "HR:Leave:020013";
	public const string LeaveOverlap = "HR:Leave:020014";
	public const string LeavePolicyNotFound = "HR:Leave:020015";
	public const string LeaveMaxDaysExceeded = "HR:Leave:020016";
	public const string LeaveInsufficientBalance = "HR:Leave:020017";
	public const string LeaveNoticePeriod = "HR:Leave:020018";
	public const string LeavePolicyInactive = "HR:Leave:020019";
	public const string LeaveApproverNotInReportingChain = "HR:Leave:020020";
	public const string LeaveReportingManagerCycle = "HR:Leave:020021";
	public const string LeaveAlreadyCancelled = "HR:Leave:020022";
	public const string LeaveNotCancellable = "HR:Leave:020023";
	public const string LeaveConflictTerminalState = "HR:Leave:020024";
	public const string LeaveCannotActOnLeaveState = "HR:Leave:020025";
	public const string LeaveInvalidMonthForAcademicYear = "HR:Leave:020026";
	public const string LeaveAttachmentNotFound = "HR:Leave:020027";
	public const string LeaveDatesOutsideAcademicYear = "HR:Leave:020028";

	// Attendance — Validation
	public const string AttendanceInvalidDateFormat = "HR:Attendance:020301";
	public const string AttendanceEndDateBeforeStartDate = "HR:Attendance:020302";
	public const string AttendanceDateRangeTooLarge = "HR:Attendance:020303";

	// Leave — File validation
	public const string LeaveFileTooLarge = "HR:Leave:020101";
	public const string LeaveFileEmpty = "HR:Leave:020102";
	public const string LeaveFileInvalidExtension = "HR:Leave:020103";
	public const string LeaveFileNameTooLong = "HR:Leave:020104";
	public const string LeaveFileInvalidCharacters = "HR:Leave:020105";
	public const string LeaveFileTooSmall = "HR:Leave:020106";
	public const string LeaveFileContentMismatch = "HR:Leave:020107";
	public const string LeaveFileInvalidAttachment = "HR:Leave:020108";

	/// <summary>Tenant context is required (e.g. custom attendance rules are tenant-scoped).</summary>
	public const string AttendanceRuleTenantRequired = "HR:Leave:020109";

	// Attendance import
	public const string AttendanceImportFileEmpty = "HR:Leave:020201";
	public const string AttendanceImportInvalidExtension = "HR:Leave:020202";
	public const string AttendanceImportMalformedFile = "HR:Leave:020203";
	public const string AttendanceImportMissingHeaders = "HR:Leave:020204";
	public const string AttendanceImportUnknownHeaders = "HR:Leave:020205";
	public const string AttendanceImportPreviewExpired = "HR:Leave:020206";
	public const string AttendanceImportPreviewInvalid = "HR:Leave:020207";
	public const string AttendanceImportNoValidRows = "HR:Leave:020208";
	public const string AttendanceImportFileTooLarge = "HR:Leave:020209";

	// ── Payroll: 03xxxx ──────────────────────────────────────────

	public const string PayrollAlreadyLocked = "HR:Payroll:030001";
	public const string PayrollNoEmployees = "HR:Payroll:030002";
	public const string SalarySlipNotFound = "HR:Payroll:030003";
	public const string SalarySlipAlreadyFinalized = "HR:Payroll:030004";
	public const string InvalidMonthYearFormat = "HR:Payroll:030005";
	public const string MonthYearRequired = "HR:Payroll:030006";
	public const string PayrollStructureNotFound = "HR:Payroll:030007";
	public const string PayrollNoStructureAssigned = "HR:Payroll:030008";

	// ── Calendar: 04xxxx ─────────────────────────────────────────

	public const string EventTypeNotFound = "HR:Calendar:040001";
	public const string EventMeetingDateRequired = "HR:Calendar:040002";
	public const string EventMeetingDateNotAllowed = "HR:Calendar:040003";
	public const string HolidayTypeNotFound = "HR:Calendar:040004";

	// ── Reports: 05xxxx ──────────────────────────────────────────

	public const string ReportExportFailed = "HR:Reports:050001";
}
