using ShikshaMitra.HR.Payroll.Settings;

namespace ShikshaMitra.HR.Employees.Payroll;

public sealed record EmployeePayrollResolvedLine<TMaster>(
	SalaryComponentContract Line,
	TMaster? Master,
	EmployeePayrollComponent? ExistingPayrollComponent)
	where TMaster : class;
