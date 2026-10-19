using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.Payroll.Settings;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

/// <summary>
/// Represents a monthly salary slip for an employee with earnings and deductions.
/// Immutable once generated to maintain payroll history integrity.
/// </summary>
public class SalarySlip : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }

	public Guid EmployeeId { get; private set; }

	public string MonthYear { get; private set; } = string.Empty;

	public int Year { get; private set; }

	public int Month { get; private set; }

	public decimal TotalEarning { get; private set; }

	public decimal TotalDeduction { get; private set; }

	public decimal NetSalary { get; private set; }

	public SalarySlipStatus Status { get; private set; }

	public DateTime ProcessedDate { get; private set; }
	public DateTimeOffset? PaidAt { get; private set; }
	public string? PaymentReference { get; private set; }
	public Guid? PaymentRecordedBy { get; private set; }
	public Guid? PaymentRequestId { get; private set; }

	public string? Remarks { get; private set; }

	public List<SalarySlipLineItem> Earnings { get; private set; } = new();

	public List<SalarySlipLineItem> Deductions { get; private set; } = new();
	public List<SalarySlipLineItem> EmployerContributions { get; private set; } = new();
	public decimal EmployerContributionTotal { get; private set; }
	public Guid? ProvidentFundRuleId { get; private set; }
	public int? ProvidentFundRuleVersion { get; private set; }
	public Guid? ProfessionalTaxRuleId { get; private set; }
	public int? ProfessionalTaxRuleVersion { get; private set; }
	public Guid? EmployeeStateInsuranceRuleId { get; private set; }
	public Guid? EsiConfigurationId { get; private set; }
	public string? StatutoryApplicationJson { get; private set; }
	internal void SetStatutoryApplication(string? evidence)
	{
		if (IsImmutable()) throw new InvalidOperationException("Issued payroll evidence cannot change.");
		StatutoryApplicationJson = evidence;
	}
	public int? EmployeeStateInsuranceRuleVersion { get; private set; }
	public decimal EmployeeStateInsuranceWages { get; private set; }
	public decimal EmployeeStateInsuranceGovernmentContribution { get; private set; }
	public string? EmployeeStateInsuranceCoverageDecision { get; private set; }
	public string? EmployeeStateInsuranceNumber { get; private set; }
	public DateTime? EmployeeStateInsuranceContributionPeriodStart { get; private set; }
	public DateTime? EmployeeStateInsuranceContributionPeriodEnd { get; private set; }
	public Guid? CompensationRevisionId { get; private set; }
	public int? CompensationRevisionNumber { get; private set; }
	public string? CompensationSalaryBasis { get; private set; }
	public string? CompensationProtectionPolicy { get; private set; }
	public decimal? CompensationQuotedAmount { get; private set; }
	public decimal? CompensationProtectedAmount { get; private set; }
	public decimal? CompensationNormalizedProtectedAmount { get; private set; }
	public decimal? CompensationTargetDifference { get; private set; }

	/// <summary>
	/// Payroll day basis used for LOP proration (denominator days).
	/// </summary>
	public decimal? PayrollBasisDays { get; private set; }

	/// <summary>
	/// Payable days from attendance (present + half-days as 0.5).
	/// </summary>
	public decimal? PayableDays { get; private set; }

	/// <summary>
	/// Applied LOP factor (capped at 1).
	/// </summary>
	public decimal? ProrationFactor { get; private set; }

	/// <summary>
	/// Day calculation mode active when the slip was computed.
	/// </summary>
	public DayCalculationType? PayrollDayCalculationMode { get; private set; }

	public Guid? PayrollPolicyId { get; private set; }
	public int? PayrollPolicyVersion { get; private set; }
	public Guid? AttendancePolicyId { get; private set; }
	public int? AttendancePolicyVersion { get; private set; }
	public Guid? WorkCalendarPolicyId { get; private set; }
	public int? WorkCalendarPolicyVersion { get; private set; }

	private SalarySlip() { } // For ORM

	internal SalarySlip(
		Guid id,
		Guid? tenantId,
		Guid employeeId,
		string monthYear,
		List<SalarySlipLineItem> earnings,
		List<SalarySlipLineItem> deductions,
		DateTime processedDate,
		string? remarks = null) : base(id)
	{
		TenantId = tenantId;
		EmployeeId = employeeId;
		SetMonthYear(monthYear);
		Earnings = earnings ?? new List<SalarySlipLineItem>();
		Deductions = deductions ?? new List<SalarySlipLineItem>();
		Remarks = remarks;
		Status = SalarySlipStatus.Draft;
		ProcessedDate = processedDate;

		RecalculateTotals();
	}

	private void RecalculateTotals()
	{
		TotalEarning = Earnings.Sum(e => e.Amount);
		TotalDeduction = Deductions.Sum(d => d.Amount);
		NetSalary = TotalEarning - TotalDeduction;
	}

	private void SetMonthYear(string monthYear)
	{
		if (string.IsNullOrWhiteSpace(monthYear))
			throw new ArgumentException("MonthYear cannot be empty", nameof(monthYear));

		if (!DateTime.TryParseExact(monthYear + "-01", "yyyy-MM-dd", null,
			System.Globalization.DateTimeStyles.None, out var date))
		{
			throw new ArgumentException("MonthYear must be in YYYY-MM format", nameof(monthYear));
		}

		MonthYear = monthYear;
		Year = date.Year;
		Month = date.Month;
	}

	public void FinalizeSlip()
	{
		if (IsImmutable())
			throw new InvalidOperationException("Salary slip is already finalized");
		Status = SalarySlipStatus.Finalized;
	}

	public void UpdateRemarks(string? remarks)
	{
		if (IsImmutable())
			throw new InvalidOperationException("Cannot update finalized or paid salary slip");

		Remarks = remarks;
	}

	internal void BeginUnpaidCorrection()
	{
		if (Status != SalarySlipStatus.Finalized)
			throw new Volo.Abp.BusinessException(HRErrorCodes.PayslipConflict);
		Status = SalarySlipStatus.Draft;
	}

	public void RecordPayment(DateTimeOffset paidAt, string reference, Guid actor, Guid requestId)
	{
		if (string.IsNullOrWhiteSpace(reference)) throw new Volo.Abp.BusinessException(HRErrorCodes.PayslipConflict);
		// PostgreSQL stores microseconds; normalize before both persisting and comparing a retry.
		var utc = paidAt.ToUniversalTime();
		paidAt = new DateTimeOffset(utc.Ticks - utc.Ticks % 10, TimeSpan.Zero);
		if (Status == SalarySlipStatus.Paid && PaymentRequestId == requestId && PaidAt == paidAt && PaymentReference == reference.Trim())
			return;
		if (Status != SalarySlipStatus.Finalized || string.IsNullOrWhiteSpace(reference) || reference.Trim().Length > 200 ||
			actor == Guid.Empty || requestId == Guid.Empty)
			throw new Volo.Abp.BusinessException(HRErrorCodes.PayslipConflict);
		PaidAt = paidAt.ToUniversalTime();
		PaymentReference = reference.Trim();
		PaymentRecordedBy = actor;
		PaymentRequestId = requestId;
		Status = SalarySlipStatus.Paid;
	}

	internal void UpdateLineItems(
		List<SalarySlipLineItem> earnings,
		List<SalarySlipLineItem> deductions,
		DateTime processedDate)
	{
		if (IsImmutable())
			throw new InvalidOperationException("Cannot update finalized or paid salary slip");

		Earnings = earnings ?? new List<SalarySlipLineItem>();
		Deductions = deductions ?? new List<SalarySlipLineItem>();
		ProcessedDate = processedDate;
		RecalculateTotals();
	}

	/// <summary>
	/// Stores payroll day basis snapshot for audit (draft only).
	/// </summary>
	internal void SetPayrollComputationSnapshot(
		decimal basisDays,
		decimal payableDays,
		decimal factor,
		DayCalculationType dayMode)
	{
		if (IsImmutable())
			throw new InvalidOperationException("Cannot update finalized or paid salary slip");

		PayrollBasisDays = basisDays;
		PayableDays = payableDays;
		ProrationFactor = factor;
		PayrollDayCalculationMode = dayMode;
	}

	internal void SetPolicySnapshot(
		Guid? payrollPolicyId,
		int? payrollPolicyVersion,
		Guid? attendancePolicyId,
		int? attendancePolicyVersion,
		Guid? workCalendarPolicyId,
		int? workCalendarPolicyVersion)
	{
		if (IsImmutable())
			throw new InvalidOperationException("Cannot update finalized or paid salary slip");

		PayrollPolicyId = payrollPolicyId;
		PayrollPolicyVersion = payrollPolicyVersion;
		AttendancePolicyId = attendancePolicyId;
		AttendancePolicyVersion = attendancePolicyVersion;
		WorkCalendarPolicyId = workCalendarPolicyId;
		WorkCalendarPolicyVersion = workCalendarPolicyVersion;
	}

	internal void SetStatutorySnapshot(
		List<SalarySlipLineItem> employerContributions,
		Guid? providentFundRuleId,
		int? providentFundRuleVersion,
		Guid? professionalTaxRuleId,
		int? professionalTaxRuleVersion,
		Guid? employeeStateInsuranceRuleId,
		int? employeeStateInsuranceRuleVersion,
		decimal employeeStateInsuranceWages,
		decimal employeeStateInsuranceGovernmentContribution,
		string? employeeStateInsuranceCoverageDecision,
		string? employeeStateInsuranceNumber,
		DateTime? employeeStateInsuranceContributionPeriodStart,
		DateTime? employeeStateInsuranceContributionPeriodEnd,
		Guid? esiConfigurationId = null)
	{
		if (IsImmutable())
			throw new InvalidOperationException("Cannot update finalized or paid salary slip");

		EmployerContributions = employerContributions ?? new List<SalarySlipLineItem>();
		EmployerContributionTotal = EmployerContributions.Sum(x => x.Amount);
		ProvidentFundRuleId = providentFundRuleId;
		ProvidentFundRuleVersion = providentFundRuleVersion;
		ProfessionalTaxRuleId = professionalTaxRuleId;
		ProfessionalTaxRuleVersion = professionalTaxRuleVersion;
		EmployeeStateInsuranceRuleId = employeeStateInsuranceRuleId;
		EsiConfigurationId = esiConfigurationId;
		EmployeeStateInsuranceRuleVersion = employeeStateInsuranceRuleVersion;
		EmployeeStateInsuranceWages = employeeStateInsuranceWages;
		EmployeeStateInsuranceGovernmentContribution = employeeStateInsuranceGovernmentContribution;
		EmployeeStateInsuranceCoverageDecision = employeeStateInsuranceCoverageDecision;
		EmployeeStateInsuranceNumber = employeeStateInsuranceNumber;
		EmployeeStateInsuranceContributionPeriodStart = employeeStateInsuranceContributionPeriodStart;
		EmployeeStateInsuranceContributionPeriodEnd = employeeStateInsuranceContributionPeriodEnd;
	}

	internal void SetCompensationSnapshot(
		Guid? revisionId,
		int? revisionNumber,
		string? salaryBasis,
		string? protectionPolicy,
		decimal? quotedAmount,
		decimal? protectedAmount,
		decimal? normalizedProtectedAmount,
		decimal? targetDifference)
	{
		if (IsImmutable())
			throw new InvalidOperationException("Cannot update finalized or paid salary slip");

		CompensationRevisionId = revisionId;
		CompensationRevisionNumber = revisionNumber;
		CompensationSalaryBasis = salaryBasis;
		CompensationProtectionPolicy = protectionPolicy;
		CompensationQuotedAmount = quotedAmount;
		CompensationProtectedAmount = protectedAmount;
		CompensationNormalizedProtectedAmount = normalizedProtectedAmount;
		CompensationTargetDifference = targetDifference;
	}

	private bool IsImmutable() => Status is SalarySlipStatus.Finalized or SalarySlipStatus.Paid;
}
