namespace ShikshaMitra.HR.Payroll.RunPayroll;

public static class ReviewStatuses
{
	public const string Pending = "PENDING";
	public const string Reviewed = "REVIEWED";
	public const string Locked = "LOCKED";
}

public static class IssueCodes
{
	public const string PfWageBasisRequired = "PF_WAGE_BASIS_REQUIRED";
	public const string MissingSalaryStructure = "MISSING_SALARY_STRUCTURE";
	public const string NoAttendance = "NO_ATTENDANCE";
	public const string LowAttendance = "LOW_ATTENDANCE";
	public const string NegativeNetPay = "NEGATIVE_NET_PAY";
	public const string NoPreviousMonthComparison = "NO_PREVIOUS_MONTH_COMPARISON";
	public const string LargeSalaryVariance = "LARGE_SALARY_VARIANCE";
	public const string AttendancePolicyEffectiveBoundary = "ATTENDANCE_POLICY_EFFECTIVE_BOUNDARY";
	public const string AttendanceSalaryDeductionConfiguration = "ATTENDANCE_SALARY_DEDUCTION_CONFIGURATION";
	public const string PayrollPolicyConfiguration = "PAYROLL_POLICY_CONFIGURATION";
	public const string NoWorkingDays = "NO_WORKING_DAYS";
	public const string EsiEstablishmentInactive = "ESI_ESTABLISHMENT_INACTIVE";
	public const string EsiWageClassificationRequired = "ESI_WAGE_CLASSIFICATION_REQUIRED";
	public const string EsiEnrollmentReviewRequired = "ESI_ENROLLMENT_REVIEW_REQUIRED";
	public const string EsiInsuranceNumberRequired = "ESI_INSURANCE_NUMBER_REQUIRED";
	public const string CompensationRevisionRequired = "COMPENSATION_REVISION_REQUIRED";
	public const string CompensationTargetVariance = "COMPENSATION_TARGET_VARIANCE";
}
