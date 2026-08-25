using System;
using System.Collections.Generic;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

/// <summary>
/// DTO for salary slip details with earnings and deductions breakdown
/// Optimized for frontend consumption
/// </summary>
public class SalarySlipDto
{
	/// <summary>
	/// Salary slip identifier
	/// </summary>
	public Guid Id { get; set; }

	/// <summary>
	/// Employee identifier
	/// </summary>
	public Guid EmployeeId { get; set; }

	/// <summary>
	/// Employee name (for display purposes)
	/// </summary>
	public string? EmployeeName { get; set; }

	/// <summary>
	/// Employee code
	/// </summary>
	public string? EmployeeCode { get; set; }

	public string? Department { get; set; }

	public string? Designation { get; set; }

	public DateOnly? DateOfJoining { get; set; }

	public string? SlipNumber { get; set; }

	/// <summary>
	/// Month and year in YYYY-MM format
	/// </summary>
	public string MonthYear { get; set; } = string.Empty;

	/// <summary>
	/// Total earning amount
	/// </summary>
	public decimal TotalEarning { get; set; }

	/// <summary>
	/// Total deduction amount
	/// </summary>
	public decimal TotalDeduction { get; set; }

	/// <summary>
	/// Net salary (TotalEarning - TotalDeduction)
	/// </summary>
	public decimal NetSalary { get; set; }

	/// <summary>
	/// List of earnings breakdown
	/// </summary>
	public List<SalarySlipLineItemDto> Earnings { get; set; } = new();

	/// <summary>
	/// List of deductions breakdown
	/// </summary>
	public List<SalarySlipLineItemDto> Deductions { get; set; } = new();
	public List<SalarySlipLineItemDto> EmployerContributions { get; set; } = new();
	public decimal EmployerContributionTotal { get; set; }
	public Guid? ProvidentFundRuleId { get; set; }
	public int? ProvidentFundRuleVersion { get; set; }
	public Guid? ProfessionalTaxRuleId { get; set; }
	public int? ProfessionalTaxRuleVersion { get; set; }
	public Guid? EmployeeStateInsuranceRuleId { get; set; }
	public int? EmployeeStateInsuranceRuleVersion { get; set; }
	public decimal EmployeeStateInsuranceWages { get; set; }
	public decimal EmployeeStateInsuranceGovernmentContribution { get; set; }
	public string? EmployeeStateInsuranceCoverageDecision { get; set; }
	public string? EmployeeStateInsuranceNumber { get; set; }
	public DateTime? EmployeeStateInsuranceContributionPeriodStart { get; set; }
	public DateTime? EmployeeStateInsuranceContributionPeriodEnd { get; set; }
	public Guid? CompensationRevisionId { get; set; }
	public int? CompensationRevisionNumber { get; set; }
	public string? CompensationSalaryBasis { get; set; }
	public string? CompensationProtectionPolicy { get; set; }
	public decimal? CompensationQuotedAmount { get; set; }
	public decimal? CompensationProtectedAmount { get; set; }
	public decimal? CompensationNormalizedProtectedAmount { get; set; }
	public decimal? CompensationTargetDifference { get; set; }

	/// <summary>
	/// Salary slip status
	/// </summary>
	public SalarySlipStatus Status { get; set; }

	/// <summary>
	/// Optional remarks
	/// </summary>
	public string? Remarks { get; set; }

	/// <summary>
	/// Processing date
	/// </summary>
	public DateTime ProcessedDate { get; set; }

	/// <summary>
	/// Payroll basis days (denominator) when slip was computed.
	/// </summary>
	public decimal? PayrollBasisDays { get; set; }

	/// <summary>
	/// Payable days from attendance at computation time.
	/// </summary>
	public decimal? PayableDays { get; set; }

	public decimal? PresentDays { get; set; }

	public decimal? HalfWorkingDays { get; set; }

	public decimal? LopDays { get; set; }

	/// <summary>
	/// LOP proration factor applied (capped at 1).
	/// </summary>
	public decimal? ProrationFactor { get; set; }

	/// <summary>
	/// Day calculation mode: working-days, total-month-days, or manual-days.
	/// </summary>
	public string? PayrollDayCalculationMode { get; set; }

	public string? BankName { get; set; }

	public string? MaskedAccountNumber { get; set; }

	public string? IfscCode { get; set; }

	public string? UanNumber { get; set; }

	public string? EpfNumber { get; set; }

	public string? NetPayInWords { get; set; }
}
