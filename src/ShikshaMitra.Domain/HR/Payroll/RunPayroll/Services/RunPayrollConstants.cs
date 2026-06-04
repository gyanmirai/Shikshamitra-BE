namespace ShikshaMitra.HR.Payroll.RunPayroll;

public static class ReviewStatuses
{
	public const string Pending = "PENDING";
	public const string Reviewed = "REVIEWED";
	public const string Locked = "LOCKED";
}

public static class IssueCodes
{
	public const string MissingSalaryStructure = "MISSING_SALARY_STRUCTURE";
	public const string NoAttendance = "NO_ATTENDANCE";
	public const string LowAttendance = "LOW_ATTENDANCE";
	public const string NegativeNetPay = "NEGATIVE_NET_PAY";
	public const string NoPreviousMonthComparison = "NO_PREVIOUS_MONTH_COMPARISON";
	public const string LargeSalaryVariance = "LARGE_SALARY_VARIANCE";
	public const string AttendancePolicyEffectiveBoundary = "ATTENDANCE_POLICY_EFFECTIVE_BOUNDARY";
	public const string PayrollPolicyConfiguration = "PAYROLL_POLICY_CONFIGURATION";
	public const string NoWorkingDays = "NO_WORKING_DAYS";
}
