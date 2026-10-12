using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

/// <summary>
/// Persisted monthly payroll preview row for one employee.
/// Used as the source-of-truth for run-payroll screen continuity.
/// </summary>
public class PayrollRunItemSnapshot : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string MonthYear { get; private set; } = string.Empty;
	public Guid EmployeeId { get; private set; }
	public bool IsLocked { get; set; }
	public string ReviewStatus { get; set; } = "PENDING";
	public Guid? ReviewedByUserId { get; set; }
	public DateTime? ReviewedAt { get; set; }
	public string EmployeeName { get; set; } = string.Empty;
	public string EmployeeIdentifier { get; set; } = string.Empty;
	public bool IsTeaching { get; set; }
	public decimal BasicSalary { get; set; }
	public decimal GrossSalary { get; set; }
	public decimal FullGrossSalary { get; set; }
	public decimal TotalDeduction { get; set; }
	public decimal EmployerContributionTotal { get; set; }
	public decimal EmployerCost { get; set; }
	public Guid? CompensationRevisionId { get; set; }
	public int? CompensationRevisionNumber { get; set; }
	public string? CompensationSalaryBasis { get; set; }
	public string? CompensationProtectionPolicy { get; set; }
	public decimal? CompensationQuotedAmount { get; set; }
	public decimal? CompensationProtectedAmount { get; set; }
	public decimal? CompensationNormalizedProtectedAmount { get; set; }
	public decimal? CompensationTargetDifference { get; set; }
	public decimal NetPay { get; set; }
	public decimal? PreviousMonthNetPay { get; set; }
	public decimal? NetPayDelta { get; set; }
	public decimal? NetPayDeltaPercent { get; set; }
	public string IssueCodesJson { get; set; } = "[]";
	public decimal PresentDays { get; set; }
	public decimal HalfWorkingDays { get; set; }
	public decimal TotalWorkingDays { get; set; }
	public decimal PayableDays { get; set; }
	public decimal ProrationFactor { get; set; }
	public string DayCalculationMode { get; set; } = string.Empty;
	public bool UsedDefaultDayCalculation { get; set; }
	public Guid? PayrollPolicyId { get; set; }
	public int? PayrollPolicyVersion { get; set; }
	public Guid? AttendancePolicyId { get; set; }
	public int? AttendancePolicyVersion { get; set; }
	public Guid? WorkCalendarPolicyId { get; set; }
	public int? WorkCalendarPolicyVersion { get; set; }
	public string HolidayImpactJson { get; set; } = "[]";
	public decimal BasisDays { get; set; }
	public decimal PayableUnits { get; set; }
	public string PayrollAttendanceSource { get; set; } = "raw_attendance";
	public string AttendancePolicyWarningsJson { get; set; } = "[]";
	public string EarningComponentsJson { get; set; } = "[]";
	public string DeductionComponentsJson { get; set; } = "[]";
	public string EmployerContributionsJson { get; set; } = "[]";
	public Guid? ProvidentFundRuleId { get; set; }
	public int? ProvidentFundRuleVersion { get; set; }
	public Guid? ProfessionalTaxRuleId { get; set; }
	public int? ProfessionalTaxRuleVersion { get; set; }
	public string? EsiCalculationDetailsJson { get; set; }
	public string? PfEligibilityDetailsJson { get; set; }
	public Guid? EsiConfigurationId { get; set; }
	public Guid? EmployeeStateInsuranceRuleId { get; set; }
	public int? EmployeeStateInsuranceRuleVersion { get; set; }
	public decimal EmployeeStateInsuranceWages { get; set; }
	public decimal EmployeeStateInsuranceGovernmentContribution { get; set; }
	public string? EmployeeStateInsuranceCoverageDecision { get; set; }
	public string? EmployeeStateInsuranceNumber { get; set; }
	public DateTime? EmployeeStateInsuranceContributionPeriodStart { get; set; }
	public DateTime? EmployeeStateInsuranceContributionPeriodEnd { get; set; }
	public string PayrollAdjustmentsJson { get; set; } = "[]";

	private PayrollRunItemSnapshot()
	{
	}

	public PayrollRunItemSnapshot(
		Guid id,
		Guid? tenantId,
		string monthYear,
		Guid employeeId) : base(id)
	{
		TenantId = tenantId;
		MonthYear = monthYear;
		EmployeeId = employeeId;
	}

	public void MarkReviewed(Guid reviewedByUserId, DateTime reviewedAt)
	{
		ReviewStatus = "REVIEWED";
		ReviewedByUserId = reviewedByUserId;
		ReviewedAt = reviewedAt;
	}

	public void MarkPending()
	{
		if (IsLocked)
		{
			return;
		}

		ReviewStatus = "PENDING";
		ReviewedByUserId = null;
		ReviewedAt = null;
	}

	public void MarkLocked()
	{
		ShikshaMitra.HR.Payroll.Statutory.EsiPayrollReadiness.EnsureInsuranceNumber(
			EmployeeStateInsuranceRuleId, EmployeeStateInsuranceCoverageDecision, EmployeeStateInsuranceNumber);
		IsLocked = true;
		ReviewStatus = "LOCKED";
	}
}