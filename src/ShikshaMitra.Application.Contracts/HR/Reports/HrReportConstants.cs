namespace ShikshaMitra.HR.Reports;

public static class HrReportFormat
{
	public const string Pdf = "pdf";
	public const string Excel = "excel";
}

public static class HrReportChartType
{
	public const string None = "none";
	public const string Kpi = "kpi";
	public const string Bar = "bar";
	public const string Line = "line";
	public const string Donut = "donut";
}

public static class HrReportDeliveryMode
{
	public const string InApp = "inApp";
	public const string InAppAndEmail = "inAppAndEmail";
}

public static class HrReportScheduleFrequency
{
	public const string Daily = "daily";
	public const string Weekly = "weekly";
	public const string Monthly = "monthly";
}

public static class HrReportScheduleStatus
{
	public const string Active = "Active";
	public const string Paused = "Paused";
}

public static class HrReportFilterOperator
{
	public new const string Equals = "equals";
	public const string Contains = "contains";
	public const string In = "in";
	public const string GreaterThanOrEqual = "gte";
	public const string LessThanOrEqual = "lte";
}

public static class HrReportSummaryFunction
{
	public const string Count = "count";
	public const string Sum = "sum";
	public const string Average = "avg";
}

public static class HrReportTemplateVisibility
{
	public const string Private = "private";
	public const string Organization = "organization";
}

public static class HrReportTemplateStatus
{
	public const string Draft = "Draft";
	public const string Published = "Published";
	public const string Archived = "Archived";
}

public static class HrReportDataSources
{
	public const string Employee = "employee";
	public const string Payroll = "payroll";
	public const string PayrollStatement = "payroll_statement";
	public const string PayrollRegister = "payroll_register";
	public const string Attendance = "attendance";
	public const string Leave = "leave";
	public const string Policies = "policies";
	public const string Performance = "performance";
}

public static class HrReportTypes
{
	public const string EmployeeProfile = "Employee Profile";
	public const string EmployeeList = "Employee List";
	public const string JoiningLetter = "Joining Letter";
	public const string EmployeeDetail = "Employee Detail";
	public const string EmployeeContactList = "Employee Contact List";
	public const string EmployeeAllDetail = "Employee All Detail";
	public const string RelievingCertificate = "Relieving Certificate";
	public const string IncrementCertificate = "Increment Certificate";
	public const string ExperienceCertificate = "Experience Certificate";
	public const string SalaryRegister = "Salary Register";
	public const string EmployeePayslip = "Employee Payslip";
	public const string MonthlyPayrollSummary = "Monthly Payroll Summary";
	public const string AllowanceReport = "Allowance Report";
	public const string DeductionReport = "Deduction Report";
	public const string PayrollReconciliationReport = "Payroll Reconciliation Report";
	public const string LeaveEncashmentReport = "Leave Encashment Report";
	public const string FinalSettlementReport = "Final Settlement Report";
}
