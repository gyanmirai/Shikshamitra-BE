using Microsoft.AspNetCore.Http;
using ShikshaMitra.HR;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Security.Roles;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;

namespace ShikshaMitra.Common.Middleware;

public static class ApiProblemDetailsMapper
{
	private const string ValidationErrorCode = "VALIDATION_ERROR";

	public static int ResolveBusinessStatusCode(string? errorCode)
	{
		if (string.IsNullOrWhiteSpace(errorCode))
		{
			return StatusCodes.Status400BadRequest;
		}

		return errorCode switch
		{
			NotificationErrorCodes.ReviewChanged or NotificationErrorCodes.ReviewExpired or NotificationErrorCodes.ReviewConsumed or
			NotificationErrorCodes.ApprovalSetupRequired or NotificationErrorCodes.RetryRequiresReview => StatusCodes.Status409Conflict,
			NotificationErrorCodes.TemplateTestRateLimited => StatusCodes.Status429TooManyRequests,
			Library.LibraryErrorCodes.ReceivingCommandConflict or Library.LibraryErrorCodes.ReceivingPlanChanged or
				Library.LibraryErrorCodes.CatalogueConfigurationChanged => StatusCodes.Status409Conflict,
			Library.LibraryErrorCodes.StockImportPlanChanged or Library.LibraryErrorCodes.StockImportCommandConflict => StatusCodes.Status409Conflict,
			Library.LibraryErrorCodes.CustodyPlanChanged or Library.LibraryErrorCodes.CustodyCommandConflict => StatusCodes.Status409Conflict,
			Library.LibraryErrorCodes.StockVerificationPlanChanged or Library.LibraryErrorCodes.StockVerificationCommandConflict or
				Library.LibraryErrorCodes.StockVerificationInvalidState or Library.LibraryErrorCodes.StockVerificationDecisionsRequired => StatusCodes.Status409Conflict,
				Library.LibraryErrorCodes.CatalogueRevisionChanged => StatusCodes.Status409Conflict,
			Academic.Curriculum.ElectivePreferenceErrorCodes.Conflict => StatusCodes.Status409Conflict,
			Students.StudentEnrollments.Admissions.AdmissionJourneyResumeErrorCodes.Conflict => StatusCodes.Status409Conflict,
			Students.StudentEnrollments.Admissions.AdmissionFormCorrectionErrorCodes.Conflict => StatusCodes.Status409Conflict,
			Students.StudentEnrollments.Admissions.AdmissionParentRegistrationErrors.Conflict => StatusCodes.Status409Conflict,
			Students.StudentRecords.StudentRecordsErrorCodes.AppAccessConflict => StatusCodes.Status409Conflict,
			Students.StudentRecords.StudentRecordsErrorCodes.AppAccessIdentityRestricted => StatusCodes.Status403Forbidden,
			Students.StudentEnrollments.Admissions.AdmissionErrorCodes.PublicInquiryLinkNameExists => StatusCodes.Status409Conflict,
			Students.StudentEnrollments.Admissions.AdmissionParentRegistrationErrors.Unavailable => StatusCodes.Status404NotFound,
			Students.StudentErrorCodes.IdAssignmentChanged or Students.StudentErrorCodes.IdAssignmentRosterChanged or
				Students.StudentErrorCodes.IdAssignmentAlreadyIssued or Students.StudentErrorCodes.IdAssignmentIssueKeyReused or
				Students.StudentErrorCodes.IdTemplateAssigned => StatusCodes.Status409Conflict,
			Academic.Mobile.MobileSubmissionErrorCodes.SubmissionConflict or
			Academic.Mobile.MobileSubmissionErrorCodes.IdempotencyConflict => StatusCodes.Status409Conflict,
			StudentRollNumberErrorCodes.StalePreview or StudentRollNumberErrorCodes.InvalidState => StatusCodes.Status409Conflict,
			StudentRollNumberErrorCodes.AdminRequired or StudentRollNumberErrorCodes.ApproverRequired => StatusCodes.Status403Forbidden,
			RoleErrorCodes.AssignedToUsers or RoleErrorCodes.ReferencedByEmployees => StatusCodes.Status409Conflict,
			TenantSettingsDomainErrorCodes.DuplicateStandardName or
			TenantSettingsDomainErrorCodes.DuplicateDivisionName => StatusCodes.Status409Conflict,
			FeeCollectionErrorCodes.IdempotencyConflict => StatusCodes.Status409Conflict,
			AccountSettingErrorCodes.DuplicateName or AccountSettingErrorCodes.DuplicateCode or
				AccountSettingErrorCodes.ActiveRecordCannotBeDeleted or AccountSettingErrorCodes.CategoryInUse or
				AccountSettingErrorCodes.BankAccountInUse => StatusCodes.Status409Conflict,
			Finance.Fees.Charges.FinanceChargeErrorCodes.ChargeCategoryInUse => StatusCodes.Status409Conflict,
			FinanceFeeCategoryErrorCodes.CannotDeleteInUse or FinanceFeeHeadErrorCodes.CannotDeleteInUse or
				FeeStructureErrorCodes.CannotDeleteInUse or ReceiptBookErrorCodes.CannotDeleteInUse or
				ConcessionTypeErrorCodes.CannotDeleteInUse or ConcessionTypeErrorCodes.ActiveCannotBeDeleted or
				PaymentTermErrorCodes.DeleteWithReferences or PaymentTermErrorCodes.DeleteRequiresDraft or
				PaymentTermErrorCodes.DeleteBoardDefault or PaymentTermErrorCodes.DuplicateCode or
				PaymentTermErrorCodes.DuplicateName => StatusCodes.Status409Conflict,
			CollectionPolicyErrorCodes.DefaultPolicyCannotBeRemoved or CollectionPolicyErrorCodes.PolicyInUse or
				CollectionPolicyErrorCodes.ExceptionWindowAlreadyActive or
				ConcessionPolicyErrorCodes.DefaultPolicyCannotBeRemoved or ConcessionPolicyErrorCodes.PolicyInUse or
				ProrationPolicyErrorCodes.DefaultPolicyCannotBeRemoved or ProrationPolicyErrorCodes.PolicyInUse or
				RefundPolicyErrorCodes.DefaultPolicyCannotBeRemoved or RefundPolicyErrorCodes.PolicyInUse => StatusCodes.Status409Conflict,
			RefundPolicyErrorCodes.DuplicateName or RefundPolicyErrorCodes.DuplicateCode => StatusCodes.Status409Conflict,
			PaymentTermErrorCodes.NotFound or CollectionPolicyErrorCodes.PolicyNotFound or
				ConcessionPolicyErrorCodes.PolicyNotFound or ProrationPolicyErrorCodes.PolicyNotFound or
				RefundPolicyErrorCodes.PolicyNotFound => StatusCodes.Status404NotFound,
			Finance.Accounts.CashManagement.CashManagementErrorCodes.Stale or
			Finance.Accounts.CashManagement.CashManagementErrorCodes.RetryConflict or
			Finance.Accounts.CashManagement.CashManagementErrorCodes.InvalidState or
			Finance.Accounts.CashManagement.CashManagementErrorCodes.InsufficientCash or
			Finance.Accounts.CashManagement.CashManagementErrorCodes.Immutable => StatusCodes.Status409Conflict,
			Finance.Accounts.Reconciliation.ReconciliationErrorCodes.AlreadyVerified or
			Finance.Accounts.Reconciliation.ReconciliationErrorCodes.AlreadyRejected or
			Finance.Accounts.Reconciliation.ReconciliationErrorCodes.TerminalStatus => StatusCodes.Status409Conflict,
			HRErrorCodes.WorkShiftReferenced or HRErrorCodes.ShiftAssignmentProtected => StatusCodes.Status409Conflict,
			HRErrorCodes.PayrollPreviewStale or HRErrorCodes.PayrollPreviewNotReady => StatusCodes.Status409Conflict,
			HRErrorCodes.PayrollPreviewNotFound => StatusCodes.Status404NotFound,
			"PAYROLL_SUBMISSION_VERSION_CONFLICT" or "PAYROLL_SETTINGS_VERSION_CONFLICT" or "PAYROLL_COMMAND_KEY_CONFLICT" or
			"PAYROLL_EMPLOYEE_ALREADY_SUBMITTED" or "PAYROLL_REVIEW_SHEET_EXPIRED" or "PAYROLL_WORKFLOW_NOT_ACTIVE" or
			"PAYROLL_SECTION_REVIEW_OVERRIDE_REQUIRED" or "PAYROLL_SECTION_BATCHES_PENDING" or "PAYROLL_LEGACY_BATCHES_PENDING" or
			"PAYROLL_SUBMISSION_REQUIRED" or "PAYROLL_SUBMISSION_REVIEW_REQUIRED" => StatusCodes.Status409Conflict,
			"PAYROLL_SUBMISSION_NOT_FOUND" => StatusCodes.Status404NotFound,
			HRErrorCodes.LeaveEmployeeNotFound => StatusCodes.Status403Forbidden,
			HRErrorCodes.EmployeePolicyAccessRequired => StatusCodes.Status403Forbidden,
			HRErrorCodes.TakeAttendanceMediumStageOutOfScope => StatusCodes.Status403Forbidden,
			HRErrorCodes.LeaveInsufficientBalance => StatusCodes.Status400BadRequest,
			HRErrorCodes.LeaveAlreadyCancelled => StatusCodes.Status409Conflict,
			HRErrorCodes.LeaveConflictTerminalState => StatusCodes.Status409Conflict,
			HRErrorCodes.TakeAttendanceApprovedLeaveMutationNotAllowed => StatusCodes.Status409Conflict,
			HRErrorCodes.StatutoryPreviewStale => StatusCodes.Status409Conflict,
			HRErrorCodes.EsiPolicyInUse => StatusCodes.Status409Conflict,
			HRErrorCodes.CompensationPreviewStale or HRErrorCodes.PfManagementChanged => StatusCodes.Status409Conflict,
			"HR:Payroll:StatutoryProfileConflict" or "HR:Payroll:PfCoverageSaveConflict" or "HR:Payroll:CompensationSaveConflict" or
			"HR:Payroll:SalaryTemplateSaveConflict" => StatusCodes.Status409Conflict,
			HRErrorCodes.EsiConfigurationLocked or HRErrorCodes.EsiConfigurationCancellationInvalid => StatusCodes.Status409Conflict,
			HRErrorCodes.LeaveAttachmentNotFound => StatusCodes.Status404NotFound,
			HRErrorCodes.LeaveFileTooLarge => StatusCodes.Status413PayloadTooLarge,
			ValidationErrorCode => StatusCodes.Status400BadRequest,
			_ => StatusCodes.Status400BadRequest
		};
	}

	public static int NormalizeStatusCode(int currentStatus, string? errorCode)
	{
		if (string.IsNullOrWhiteSpace(errorCode) ||
			(currentStatus is StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden &&
			 ApiPermissionDeniedDetails.IsFrameworkAuthorizationCode(errorCode)))
		{
			return currentStatus;
		}

		var mappedStatus = ResolveBusinessStatusCode(errorCode);

		// For business-coded payloads, canonical mapping should always win to avoid drift
		// between exception pipeline and result-filter normalization.
		return mappedStatus;
	}

	public static string GetDefaultTitle(int statusCode, bool hasValidationErrors = false)
	{
		if (statusCode == StatusCodes.Status400BadRequest && hasValidationErrors)
		{
			return "Validation failed";
		}

		return statusCode switch
		{
			StatusCodes.Status400BadRequest => "Business rule violated",
			StatusCodes.Status401Unauthorized => "Unauthorized",
			StatusCodes.Status403Forbidden => "Forbidden",
			StatusCodes.Status404NotFound => "Resource not found",
			StatusCodes.Status409Conflict => "Conflict",
			StatusCodes.Status413PayloadTooLarge => "Payload too large",
			_ => "Request failed"
		};
	}

	public static string GetDefaultDetail(int statusCode, bool hasValidationErrors = false)
	{
		if (statusCode == StatusCodes.Status400BadRequest && hasValidationErrors)
		{
			return "One or more validation errors occurred.";
		}

		return statusCode switch
		{
			StatusCodes.Status400BadRequest => "One or more business rules were violated.",
			StatusCodes.Status401Unauthorized => "Authentication is required to access this resource.",
			StatusCodes.Status403Forbidden => ApiPermissionDeniedDetails.DefaultDetail,
			StatusCodes.Status404NotFound => "The requested resource was not found.",
			StatusCodes.Status409Conflict => "The request could not be completed because of a conflict.",
			StatusCodes.Status413PayloadTooLarge => "The request payload is too large.",
			_ => "The request failed."
		};
	}
}
