using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Concessions;
using ShikshaMitra.Finance.Fees.FeeAdjustment;
using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Fees.FeeCollection;

/// <summary>
/// Represents a single monthly fee obligation for a student.
/// All monetary mutations must go through <see cref="ApplyPayment"/> and <see cref="ApplyRefund"/>
/// — never assign <see cref="PaidAmount"/> or <see cref="RefundedAmount"/> directly.
/// </summary>
public class DueFee : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid StudentId { get; private set; }
	public Guid FeeHeadId { get; private set; }
	public MonthEnum Month { get; private set; }
	public int Year { get; private set; }
	public bool IsOneTime { get; private set; }
	public double OriginalAmount { get; private set; }
	public double NetAmount { get; private set; }
	public double PaidAmount { get; private set; }
	public double OutstandingAmount { get; private set; }

	/// <summary>Total amount already refunded against this obligation line.</summary>
	public double RefundedAmount { get; private set; }

	/// <summary>Total adjustment credit applied to this obligation line (reduces outstanding).</summary>
	public double AdjustmentCreditAmount { get; private set; }

	public FeeStatus Status { get; private set; }

	private DueFee() { /* For ORM */ }

	public DueFee(
		Guid id,
		Guid? tenantId,
		Guid studentId,
		Guid feeHeadId,
		MonthEnum month,
		int year,
		bool isOneTime,
		double amount)
		: base(id)
	{
		TenantId = tenantId;
		StudentId = studentId;
		FeeHeadId = feeHeadId;
		Month = month;
		Year = year;
		IsOneTime = isOneTime;
		OriginalAmount = amount;
		NetAmount = 0;
		PaidAmount = 0;
		OutstandingAmount = amount;
		RefundedAmount = 0;
		AdjustmentCreditAmount = 0;
		Status = FeeStatus.Pending;
	}

	/// <summary>
	/// Records a payment allocation against this obligation line.
	/// Calling code is responsible for ensuring the sum of allocations matches the receipt total.
	/// </summary>
	/// <param name="amount">Positive amount being allocated to this line.</param>
	public void ApplyPayment(double amount)
	{
		if (amount <= 0)
			throw new BusinessException(DueFeeErrorCodes.PaymentAmountMustBePositive)
				.WithData("Amount", amount);

		PaidAmount += amount;
		RecalculateOutstanding();
		Status = OutstandingAmount <= 0d ? FeeStatus.Paid : FeeStatus.Partial;
	}

	/// <summary>
	/// Records a refund against this obligation line (called when a refund request is approved).
	/// Invariant: <see cref="RefundedAmount"/> + <paramref name="amount"/> must not exceed
	/// <see cref="PaidAmount"/>.
	/// </summary>
	/// <param name="amount">Positive amount being refunded from this line.</param>
	public void ApplyRefund(double amount)
	{
		if (amount <= 0)
			throw new BusinessException(DueFeeErrorCodes.RefundAmountMustBePositive)
				.WithData("Amount", amount);

		var available = PaidAmount - RefundedAmount;
		if (amount > available)
			throw new BusinessException(DueFeeErrorCodes.RefundExceedsPaidAmount)
				.WithData("Requested", amount)
				.WithData("Available", available);

		RefundedAmount += amount;
	}

	/// <summary>Net refundable balance: paid minus already refunded.</summary>
	public double RefundableBalance => Math.Max(0d, PaidAmount - RefundedAmount);

	/// <summary>
	/// Credit available to fund adjustments from this line (overpayment / paid credit not yet allocated).
	/// </summary>
	public double AdjustableBalance => Math.Max(0d, OutstandingAmount);

	/// <summary>
	/// Reverses a payment allocation against this obligation line (called when
	/// the payment instrument — e.g. cheque — is rejected/bounced).
	/// Invariant: cannot reverse more than <see cref="PaidAmount"/>.
	/// </summary>
	/// <param name="amount">Positive amount being reversed.</param>
	public void ReversePayment(double amount)
	{
		if (amount <= 0)
			throw new BusinessException(DueFeeErrorCodes.ReverseAmountMustBePositive)
				.WithData("Amount", amount);

		if (amount > PaidAmount)
			throw new BusinessException(DueFeeErrorCodes.ReverseExceedsPaidAmount)
				.WithData("Requested", amount)
				.WithData("PaidAmount", PaidAmount);

		PaidAmount -= amount;
		RecalculateOutstanding();
		Status = PaidAmount <= 0d ? FeeStatus.Pending : FeeStatus.Partial;
	}

	/// <summary>
	/// Applies an approved concession discount to this monthly obligation line.
	/// </summary>
	public void ApplyConcession(double concessionAmount)
	{
		if (concessionAmount <= 0)
		{
			throw new BusinessException(DueFeeErrorCodes.PaymentAmountMustBePositive)
				.WithData("Amount", concessionAmount);
		}

		if (concessionAmount > OutstandingAmount)
		{
			throw new BusinessException(FinanceConcessionErrorCodes.ConcessionExceedsDueOutstanding)
				.WithData("DueFeeId", Id)
				.WithData("Outstanding", OutstandingAmount)
				.WithData("Requested", concessionAmount);
		}

		NetAmount += concessionAmount;
		RecalculateOutstanding();
	}

	/// <summary>
	/// Applies an approved fee adjustment credit to this obligation line (reduces outstanding).
	/// </summary>
	public void ApplyAdjustmentCredit(double amount)
	{
		if (amount <= 0)
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
		RecalculateOutstanding();
	}

	private void RecalculateOutstanding() =>
		OutstandingAmount = Math.Max(0d, OriginalAmount - PaidAmount - NetAmount);
}
