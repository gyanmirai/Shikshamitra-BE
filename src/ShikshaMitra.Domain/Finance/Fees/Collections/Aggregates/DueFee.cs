using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Concessions;
using ShikshaMitra.Finance.Fees.FeeAdjustment;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Domain.Finance.Policies;
using System;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Fees.FeeCollection;

/// <summary>
/// A single fee obligation for a student — either a legacy monthly slice or a
/// Payment Term installment snapshot. Monetary mutations go through ApplyPayment/ApplyRefund.
/// </summary>
public class DueFee : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid? StudentId { get; private set; }
	public Guid? FinanceFeeAccountId { get; private set; }
	public void LinkStudent(Guid studentId) => StudentId = FinanceFeePayerIdentity.LinkStudent(StudentId, studentId);
	public void BindFinanceFeeAccount(Guid accountId) => FinanceFeeAccountId =
		FinanceFeePayerIdentity.BindAccount(FinanceFeeAccountId, accountId);
    public decimal? AllocationPercentage { get; private set; }
	public void PreserveAllocation(decimal percentage)
	{
		if (percentage < 0 || percentage > 100 || AllocationPercentage.HasValue) throw new ArgumentOutOfRangeException(nameof(percentage));
		AllocationPercentage = percentage;
	}
	public Guid? StudentFeeStructureId { get; private set; }
    public void BindAssignment(Guid assignmentId)
    {
        if (StudentFeeStructureId == assignmentId && assignmentId != Guid.Empty) return;
        if (assignmentId == Guid.Empty || StudentFeeStructureId.HasValue)
            throw new ArgumentException("A due can be bound to an assignment only once.");
        StudentFeeStructureId = assignmentId;
    }
	public Guid FeeHeadId { get; private set; }
	public MonthEnum Month { get; private set; }
	public int Year { get; private set; }
	public bool IsOneTime { get; private set; }

	/// <summary>
	/// The date this obligation becomes overdue — feeds LateFeeCalculationService's daysLate
	/// input (Finance/Policies). No per-school "due day of month" setting exists yet, so this
	/// is computed by the caller (DueFeeManager) using DueFeeManager.DefaultDueDayOfMonth; a
	/// real per-school configuration is a documented follow-up, not silently assumed elsewhere.
	/// </summary>
	public DateTime DueDate { get; private set; }
	public decimal OriginalAmount { get; private set; }
	/// <summary>Installment amount before proration.</summary>
	public decimal ScheduledAmount { get; private set; }
	public decimal NetAmount { get; private set; }
	public decimal PaidAmount { get; private set; }
	public decimal OutstandingAmount { get; private set; }

	// Stored in the aggregate's existing JSON extension column; independent of principal.
	public decimal GetAssessedLateFeeAmount() => this.GetProperty<decimal>("AssessedLateFeeAmount", 0m);
	public string GetLateFeeAssessmentExplanation() => this.GetProperty<string>("LateFeeAssessmentExplanation", string.Empty);

	public void PreserveLateFeeAssessment(decimal amount, DateTimeOffset assessedAt, Guid policyVersionId, string explanation)
	{
		if (amount < 0m) throw new ArgumentOutOfRangeException(nameof(amount));
		if (policyVersionId == Guid.Empty) throw new ArgumentException("The collection policy version is required.", nameof(policyVersionId));
		if (amount <= GetAssessedLateFeeAmount()) return;
		this.SetProperty("AssessedLateFeeAmount", amount);
		this.SetProperty("LateFeeAssessedAt", assessedAt);
		this.SetProperty("LateFeeAssessmentPolicyVersionId", policyVersionId);
		this.SetProperty("LateFeeAssessmentExplanation", explanation);
	}

	/// <summary>Total amount already refunded against this obligation line.</summary>
	public decimal RefundedAmount { get; private set; }

	/// <summary>Total concession credit applied to this obligation line.</summary>
	public decimal ConcessionAmount { get; private set; }

	/// <summary>Total adjustment credit applied to this obligation line (reduces outstanding).</summary>
	public decimal AdjustmentCreditAmount { get; private set; }

	public FeeStatus Status { get; private set; }

	// ── Payment Term schedule snapshot ────────────────────────────────────────

	public Guid? FeeStructureDetailId { get; private set; }
	public Guid? PaymentTermId { get; private set; }
	public Guid? PaymentTermVersionId { get; private set; }
	public Guid? PaymentTermInstallmentId { get; private set; }
	public int? InstallmentSequence { get; private set; }
	public string? InstallmentName { get; private set; }
	public DateTime? CoverageStartDate { get; private set; }
	public DateTime? CoverageEndDate { get; private set; }
	public DateTime? DemandDate { get; private set; }
	public DateTime? ScheduleDueDate { get; private set; }
	public ScheduleSource ScheduleSource { get; private set; }

	// ── Proration calculation snapshot ────────────────────────────────────────
	public Guid? ProrationPolicyId { get; private set; }
	public Guid? ProrationPolicyVersionId { get; private set; }
	public DateTime? ProrationChargeStartDate { get; private set; }
	public DateTime? ProrationChargeEndDate { get; private set; }
	public DateTime? ProrationEligibleStartDate { get; private set; }
	public DateTime? ProrationEligibleEndDate { get; private set; }
	public int? ProrationCoverageDays { get; private set; }
	public int? ProrationEligibleDays { get; private set; }
	public int? ProrationChargedDays { get; private set; }
	public decimal? ProrationFactor { get; private set; }
	public ProrationRuleType? ProrationRuleType { get; private set; }
	public ProrationRoundingMode? ProrationRoundingMode { get; private set; }
	public string? ProrationExplanation { get; private set; }

	/// <summary>
	/// Stable uniqueness key for idempotent generation.
	/// Legacy: <c>{feeHeadId}:{year}:{month}</c>
	/// PaymentTerm: <c>{feeStructureDetailId}:{versionId}:{sequence}</c>
	/// </summary>
	public string ScheduleInstanceKey { get; private set; } = string.Empty;

	private DueFee() { /* For ORM */ }

public DueFee(
		Guid id,
		Guid? tenantId,
		Guid? studentId,
		Guid feeHeadId,
		MonthEnum month,
		int year,
		bool isOneTime,
		decimal amount,
		DateTime dueDate,
		Guid? feeStructureDetailId = null,
		Guid? paymentTermId = null,
		Guid? paymentTermVersionId = null,
		Guid? paymentTermInstallmentId = null,
		int? installmentSequence = null,
		string? installmentName = null,
		DateTime? coverageStartDate = null,
		DateTime? coverageEndDate = null,
		DateTime? demandDate = null,
		DateTime? scheduleDueDate = null,
		ScheduleSource scheduleSource = ScheduleSource.Legacy,
		decimal? scheduledAmount = null,
		Guid? prorationPolicyId = null,
		Guid? prorationPolicyVersionId = null,
		DateTime? prorationChargeStartDate = null,
		DateTime? prorationChargeEndDate = null,
		DateTime? prorationEligibleStartDate = null,
		DateTime? prorationEligibleEndDate = null,
		int? prorationCoverageDays = null,
		int? prorationEligibleDays = null,
		int? prorationChargedDays = null,
		decimal? prorationFactor = null,
		ProrationRuleType? prorationRuleType = null,
		ProrationRoundingMode? prorationRoundingMode = null,
		string? prorationExplanation = null,
		Guid? financeFeeAccountId = null)
		: base(id)
	{
		TenantId = tenantId;
		FinanceFeePayerIdentity.Validate(studentId, financeFeeAccountId);
		StudentId = studentId;
		FinanceFeeAccountId = financeFeeAccountId;
		FeeHeadId = feeHeadId;
		Month = month;
		Year = year;
		IsOneTime = isOneTime;
		DueDate = dueDate;
		OriginalAmount = amount;
		ScheduledAmount = scheduledAmount ?? amount;
		NetAmount = 0m;
		PaidAmount = 0m;
		OutstandingAmount = amount;
		RefundedAmount = 0m;
		ConcessionAmount = 0m;
		AdjustmentCreditAmount = 0m;
		Status = FeeStatus.Pending;

		FeeStructureDetailId = feeStructureDetailId;
		PaymentTermId = paymentTermId;
		PaymentTermVersionId = paymentTermVersionId;
		PaymentTermInstallmentId = paymentTermInstallmentId;
		InstallmentSequence = installmentSequence;
		InstallmentName = string.IsNullOrWhiteSpace(installmentName) ? null : installmentName.Trim();
		CoverageStartDate = coverageStartDate?.Date;
		CoverageEndDate = coverageEndDate?.Date;
		DemandDate = demandDate?.Date;
		ScheduleDueDate = scheduleDueDate?.Date;
		ScheduleSource = scheduleSource;
		if (prorationPolicyId.HasValue != prorationPolicyVersionId.HasValue)
			throw new ArgumentException("Proration policy and version must both be present or absent.");
		ProrationPolicyId = prorationPolicyId;
		ProrationPolicyVersionId = prorationPolicyVersionId;
		ProrationChargeStartDate = prorationChargeStartDate?.Date;
		ProrationChargeEndDate = prorationChargeEndDate?.Date;
		ProrationEligibleStartDate = prorationEligibleStartDate?.Date;
		ProrationEligibleEndDate = prorationEligibleEndDate?.Date;
		ProrationCoverageDays = prorationCoverageDays;
		ProrationEligibleDays = prorationEligibleDays;
		ProrationChargedDays = prorationChargedDays;
		ProrationFactor = prorationFactor;
		ProrationRuleType = prorationRuleType;
		ProrationRoundingMode = prorationRoundingMode;
		ProrationExplanation = string.IsNullOrWhiteSpace(prorationExplanation) ? null : prorationExplanation.Trim();
		ScheduleInstanceKey = BuildScheduleInstanceKey(
			scheduleSource,
			feeHeadId,
			year,
			month,
			feeStructureDetailId,
			paymentTermVersionId,
			installmentSequence);
	}

	public string GetDisplayInstallmentLabel()
	{
		if (!string.IsNullOrWhiteSpace(InstallmentName))
		{
			return InstallmentName!;
		}

		return $"{Month} {Year}";
	}

	/// <summary>
	/// A Payment Term row is created as an immutable schedule snapshot when the
	/// structure is assigned, but it becomes a generated bill only on its demand
	/// date. A scheduled row may still accept an explicitly allocated advance payment.
	/// Legacy rows remain generated immediately.
	/// </summary>
	public bool IsBillGeneratedOn(DateTime date) =>
		ScheduleSource == ScheduleSource.Legacy || !DemandDate.HasValue || DemandDate.Value.Date <= date.Date;

	public static string BuildScheduleInstanceKey(
		ScheduleSource scheduleSource,
		Guid feeHeadId,
		int year,
		MonthEnum month,
		Guid? feeStructureDetailId,
		Guid? paymentTermVersionId,
		int? installmentSequence)
	{
		if (scheduleSource == ScheduleSource.PaymentTerm &&
			feeStructureDetailId.HasValue &&
			paymentTermVersionId.HasValue &&
			installmentSequence.HasValue)
		{
			return $"{feeStructureDetailId:N}:{paymentTermVersionId:N}:{installmentSequence.Value}";
		}

		return $"{feeHeadId:N}:{year}:{(int) month}";
	}

	public void ApplyPayment(decimal amount)
	{
		if (amount <= 0m)
			throw new BusinessException(DueFeeErrorCodes.PaymentAmountMustBePositive)
				.WithData("Amount", amount);

		PaidAmount += amount;
		RecalculateBalancesAndStatus();
	}

	public void ApplyRefund(decimal amount)
	{
		if (amount <= 0m)
			throw new BusinessException(DueFeeErrorCodes.RefundAmountMustBePositive)
				.WithData("Amount", amount);

		var available = PaidAmount - RefundedAmount;
		if (amount > available)
			throw new BusinessException(DueFeeErrorCodes.RefundExceedsPaidAmount)
				.WithData("Requested", amount)
				.WithData("Available", available);

		RefundedAmount += amount;
		RecalculateBalancesAndStatus();
	}

	public void ReverseRefund(decimal amount)
	{
		if (amount <= 0m)
			throw new BusinessException(DueFeeErrorCodes.ReverseAmountMustBePositive)
				.WithData("Amount", amount);
		if (amount > RefundedAmount)
			throw new BusinessException(DueFeeErrorCodes.ReverseExceedsPaidAmount)
				.WithData("Requested", amount)
				.WithData("Available", RefundedAmount);

		RefundedAmount -= amount;
		RecalculateBalancesAndStatus();
	}

	public decimal RefundableBalance => Math.Max(0m, PaidAmount - RefundedAmount);

	/// <summary>Paid money above the remaining obligation after approved concessions and adjustments.</summary>
	public decimal OverpaymentAmount => Math.Max(0m, PaidAmount - RefundedAmount + NetAmount - OriginalAmount);

	public decimal AdjustableBalance => Math.Max(0m, OutstandingAmount);

	public void ReversePayment(decimal amount)
	{
		if (amount <= 0m)
			throw new BusinessException(DueFeeErrorCodes.ReverseAmountMustBePositive)
				.WithData("Amount", amount);

		if (amount > RefundableBalance)
			throw new BusinessException(DueFeeErrorCodes.ReverseExceedsPaidAmount)
				.WithData("Requested", amount)
				.WithData("Available", RefundableBalance);

		PaidAmount -= amount;
		RecalculateBalancesAndStatus();
	}

	public void ApplyConcession(decimal concessionAmount)
	{
		if (concessionAmount <= 0m)
		{
			throw new BusinessException(DueFeeErrorCodes.PaymentAmountMustBePositive)
				.WithData("Amount", concessionAmount);
		}

		var remainingConcessionCapacity = Math.Max(0m, OriginalAmount - NetAmount);
		if (concessionAmount > remainingConcessionCapacity)
		{
			throw new BusinessException(FinanceConcessionErrorCodes.ConcessionExceedsDueOutstanding)
				.WithData("DueFeeId", Id)
				.WithData("Available", remainingConcessionCapacity)
				.WithData("Requested", concessionAmount);
		}

		ConcessionAmount += concessionAmount;
		NetAmount += concessionAmount;
		RecalculateBalancesAndStatus();
	}

	public void ReverseConcession(decimal amount)
	{
		if (amount <= 0m)
		{
			throw new BusinessException(DueFeeErrorCodes.ReverseAmountMustBePositive)
				.WithData("Amount", amount);
		}

		if (amount > ConcessionAmount)
		{
			throw new BusinessException(FinanceConcessionErrorCodes.ReversalExceedsConcessionBalance)
				.WithData("DueFeeId", Id)
				.WithData("Requested", amount)
				.WithData("Available", ConcessionAmount);
		}

		ConcessionAmount -= amount;
		NetAmount -= amount;
		RecalculateBalancesAndStatus();
	}

	public void ApplyAdjustmentCredit(decimal amount)
	{
		if (amount <= 0m)
		{
			throw new BusinessException(DueFeeErrorCodes.PaymentAmountMustBePositive)
				.WithData("Amount", amount);
		}

		if (amount > OutstandingAmount)
		{
			throw new BusinessException(FeeAdjustmentErrorCodes.TargetExceedsOutstanding)
				.WithData("DueFeeId", Id)
				.WithData("Outstanding", OutstandingAmount)
				.WithData("Requested", amount);
		}

		AdjustmentCreditAmount += amount;
		NetAmount += amount;
		RecalculateBalancesAndStatus();
	}

	private void RecalculateBalancesAndStatus()
	{
		var netPaid = Math.Max(0m, PaidAmount - RefundedAmount);
		OutstandingAmount = Math.Max(0m, OriginalAmount - netPaid - NetAmount);
		Status = OutstandingAmount <= 0m
			? FeeStatus.Paid
			: netPaid > 0m || NetAmount > 0m
				? FeeStatus.Partial
				: FeeStatus.Pending;
	}
}
