using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Payroll.Statutory;

namespace ShikshaMitra.HR.Payroll.Services;

public sealed record PayrollComputedLineItem(
	EmployeePayrollComponent Source,
	decimal Amount)
{
	public decimal? FullMonthAmount { get; init; }
}

public sealed record PayrollComputedEmployerContribution(
	string Name,
	decimal Amount,
	Guid RuleId,
	int RuleVersion);

public sealed record PayrollStatutoryRuleSet(
	ProvidentFund? ProvidentFund,
	IReadOnlyList<ProfessionalTax> ProfessionalTaxSlabs,
	bool IsEmploymentCessationMonth = false,
	PayrollEsiStatutoryContext? EmployeeStateInsurance = null);

public sealed record PayrollEsiStatutoryContext(
	DateTime PayrollMonth,
	StatutoryRuleVersion Rule,
	StatutoryEstablishment Establishment,
	EmployeeStatutoryEnrollment? Enrollment,
	IReadOnlyDictionary<Guid, StatutoryWageTreatment> ComponentTreatments,
	IReadOnlyList<StatutoryOverride> Overrides,
	EmployeePayrollComponent DeductionSource);

public sealed record PayrollComputationResult(
	int PresentDays,
	int HalfWorkingDays,
	decimal PayableDays,
	decimal ProrationFactor,
	decimal BasicSalary,
	decimal FullGrossSalary,
	decimal GrossSalary,
	decimal TotalDeduction,
	decimal EmployerContributionTotal,
	decimal NetPay,
	List<PayrollComputedLineItem> Earnings,
	List<PayrollComputedLineItem> Deductions,
	List<PayrollComputedEmployerContribution> EmployerContributions,
	Guid? ProvidentFundRuleId,
	int? ProvidentFundRuleVersion,
	Guid? ProfessionalTaxRuleId,
	int? ProfessionalTaxRuleVersion,
	Guid? EmployeeStateInsuranceRuleId,
	int? EmployeeStateInsuranceRuleVersion,
	decimal EmployeeStateInsuranceWages,
	decimal EmployeeStateInsuranceGovernmentContribution,
	string? EmployeeStateInsuranceCoverageDecision,
	string? EmployeeStateInsuranceNumber,
	DateTime? EmployeeStateInsuranceContributionPeriodStart,
	DateTime? EmployeeStateInsuranceContributionPeriodEnd,
	List<string> StatutoryIssueCodes)
{
	public decimal TotalEarnings => NetPay + TotalDeduction;
	public decimal AdditionalEarnings => TotalEarnings - GrossSalary;
	public EsiStatutoryCalculationResult? EmployeeStateInsurance { get; init; }
	public Templates.SalaryProtectionResult? Protection { get; init; }
	public decimal StatutoryTakeHome => GrossSalary - Deductions.Where(x => x.Source.SemanticType is
		Templates.PayrollComponentSemanticType.ProfessionalTax or
		Templates.PayrollComponentSemanticType.EmployeeProvidentFund or
		Templates.PayrollComponentSemanticType.EmployeeStateInsurance).Sum(x => x.Amount);
}

public sealed record PayrollAttendancePayableDays(
	int PresentDays,
	int HalfWorkingDays,
	decimal PayableDays);

public sealed record PayrollPaidLeaveDay(
	DateOnly Date,
	decimal PayableUnits);

public sealed record PayrollLeaveUnits(
	decimal PaidUnits,
	decimal UnpaidUnits);

public sealed record PayrollApprovedAttendanceDay(
	DateOnly Date,
	EmployeeAttendanceStatusType Status,
	decimal PayableUnits);

public sealed record PayrollBasisDaysResult(
	decimal BasisDays,
	DayCalculationType EffectiveMode,
	bool UsedTenantDefault,
	SaturdayWorkPolicy SaturdayWorkPolicy = SaturdayWorkPolicy.FullDay,
	IReadOnlyDictionary<DateOnly, decimal>? WorkingUnitsByDate = null);

public sealed record PayrollResolvedHolidayDay(
	DateOnly Date,
	Guid? CalendarHolidayId,
	string HolidayName,
	bool IncludedInPayroll,
	bool IsPayrollOnly);
