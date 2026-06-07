namespace ShikshaMitra.HR;

public static partial class HRErrorCodes
{
	public const string PayrollAlreadyLocked = "HR:Payroll:030001";
	public const string PayrollNoEmployees = "HR:Payroll:030002";
	public const string SalarySlipNotFound = "HR:Payroll:030003";
	public const string SalarySlipAlreadyFinalized = "HR:Payroll:030004";
	public const string InvalidMonthYearFormat = "HR:Payroll:030005";
	public const string MonthYearRequired = "HR:Payroll:030006";
	public const string PayrollStructureNotFound = "HR:Payroll:030007";
	public const string PayrollNoStructureAssigned = "HR:Payroll:030008";
	public const string PayrollEmployeeIdRequired = "HR:Payroll:030009";
	public const string SalarySlipFetchFailed = "HR:Payroll:030010";
	public const string PayrollPreviewFailed = "HR:Payroll:030011";
	public const string PayrollReviewRequiredBeforeLock = "HR:Payroll:030013";
	public const string PayrollReviewEmployeeIdsRequired = "HR:Payroll:030014";
	public const string PayrollReviewCannotChangeLocked = "HR:Payroll:030015";
	public const string PayrollInvalidPageSize = "HR:Payroll:030016";
	public const string PayrollBatchSaveFailed = "HR:Payroll:030017";
	public const string PayrollGenerationCompleteFailure = "HR:Payroll:030018";
	public const string PayrollSnapshotMigrationRequired = "HR:Payroll:030019";
	public const string SalarySlipPdfRequiresFinalized = "HR:Payroll:030021";
	public const string PayrollAdjustmentNameRequired = "HR:Payroll:030022";
	public const string PayrollAdjustmentAmountInvalid = "HR:Payroll:030023";
	public const string PayrollAdjustmentReasonRequired = "HR:Payroll:030024";
	public const string PayrollAdjustmentNotFound = "HR:Payroll:030025";
	public const string PayrollBlockingIssuesBeforeLock = "HR:Payroll:030026";
	public const string PayrollAdjustmentRequiresReviewedPreview = "HR:Payroll:030027";
	public const string PayrollReviewContainsAdjustments = "HR:Payroll:030028";
	public const string SalarySlipEmployeeNotActiveInMonth = "HR:Payroll:030029";
	public const string PayrollAcademicYearNotFound = "HR:Payroll:030030";
	public const string PayrollMonthOutsideAcademicYear = "HR:Payroll:030031";
	public const string PayrollSnapshotRequiredForSalarySlip = "HR:Payroll:030032";
	public const string PayrollMonthLockBusy = "HR:Payroll:030033";
	public const string ProvidentFundCodeAlreadyExists = "ShikshaMitra:ProvidentFundCodeAlreadyExists";
	public const string ProvidentFundNameAlreadyExists = "ShikshaMitra:ProvidentFundNameAlreadyExists";
	public const string EarningComponentNameAlreadyExists = "ShikshaMitra:EarningComponentNameAlreadyExists";
	public const string DeductionComponentNameAlreadyExists = "ShikshaMitra:DeductionComponentNameAlreadyExists";
	public const string PayrollComponentPreviewRequired = "ShikshaMitra:PayrollComponentPreviewRequired";
	public const string PayrollComponentPreviewExpired = "ShikshaMitra:PayrollComponentPreviewExpired";
	public const string PayrollComponentInactive = "ShikshaMitra:PayrollComponentInactive";
	public const string PayrollComponentInvalidAmount = "ShikshaMitra:InvalidAmount";
	public const string PayrollComponentInvalidPercentage = "ShikshaMitra:InvalidPercentage";
	public const string PayrollComponentMissingSelectedComponent = "ShikshaMitra:MissingSelectedComponent";
}
