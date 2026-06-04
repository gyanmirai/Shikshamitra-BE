using ShikshaMitra.HR.Employees.Payroll;

namespace ShikshaMitra.HR.Payroll.SalaryStructure;

public sealed record ResolvedSalaryComponent<TMaster>(
	SalaryComponentDto Line,
	EmployeePayrollComponent? Existing,
	TMaster? Master);
