namespace ShikshaMitra.HR.Payroll.Templates;

/// <summary>Period-specific calculation evidence persisted with payroll and the salary slip.</summary>
public sealed record SalaryProtectionResult(
	string Policy,
	decimal MonthlyTarget,
	decimal PeriodTarget,
	decimal Achieved,
	decimal Difference,
	decimal RecurringGross,
	decimal GrossAdjustment,
	decimal ExcludedDeductions,
	decimal AdditionalEarnings,
	decimal AdditionalEmployeeStatutoryDeductions,
	decimal AdditionalEmployerContributions,
	string Status,
	int CalculationVersion = 1);
