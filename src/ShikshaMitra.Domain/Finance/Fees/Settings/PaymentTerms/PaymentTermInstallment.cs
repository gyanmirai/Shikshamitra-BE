using System;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms;

/// <summary>
/// A single installment within a <see cref="PaymentTermVersion"/>.
/// Immutable once the parent version is published.
/// Allocation is always a percentage of the attached fee-head annual amount
/// so the same term can be reused across structures with different fee totals.
/// </summary>
public class PaymentTermInstallment : Entity<Guid>
{
	/// <summary>FK to the parent version snapshot.</summary>
	public Guid PaymentTermVersionId { get; private set; }

	/// <summary>1-based display/install order within the version.</summary>
	public int Sequence { get; private set; }

	/// <summary>Human-readable label, e.g. "Apr-May".</summary>
	public string Name { get; private set; } = string.Empty;

	/// <summary>First day of the period this installment covers.</summary>
	public DateTime CoverageStartDate { get; private set; }

	/// <summary>Last day of the period this installment covers.</summary>
	public DateTime CoverageEndDate { get; private set; }

	/// <summary>Date on which the demand for this installment is raised.</summary>
	public DateTime DemandDate { get; private set; }

	/// <summary>Date by which this installment must be paid.</summary>
	public DateTime DueDate { get; private set; }

	/// <summary>Share of the fee-head annual amount (0–100, exclusive of 0).</summary>
	public decimal AllocationPercentage { get; private set; }

	/// <summary>
	/// Per-installment Payment Policy partial-payment exception.
	/// When explicitly set (true/false), overrides the resolved Payment Policy for this installment.
	/// Null = inherit Payment Policy.
	/// </summary>
	public bool? AllowPartialOverride { get; private set; }

	/// <summary>
	/// Per-installment Collection Policy grace-period override (in days).
	/// Null = inherit Collection Policy.
	/// </summary>
	public int? GracePeriodDaysOverride { get; private set; }

	/// <summary>Whether demand should be auto-generated for this installment.</summary>
	public bool DemandEnabled { get; private set; }

	/// <summary>Whether a reminder should be sent for this installment.</summary>
	public bool ReminderEnabled { get; private set; }

	/// <summary>Optional specific reminder date; null = derived from due date.</summary>
	public DateTime? ReminderDate { get; private set; }

	protected PaymentTermInstallment() { /* For ORM */ }

	public PaymentTermInstallment(
		Guid id,
		Guid paymentTermVersionId,
		int sequence,
		string name,
		DateTime coverageStartDate,
		DateTime coverageEndDate,
		DateTime demandDate,
		DateTime dueDate,
		decimal allocationPercentage,
		bool demandEnabled,
		bool reminderEnabled,
		DateTime? reminderDate = null,
		bool? allowPartialOverride = null,
		int? gracePeriodDaysOverride = null) : base(id)
	{
		PaymentTermVersionId = paymentTermVersionId;
		Sequence = sequence;
		Name = name;
		CoverageStartDate = coverageStartDate.Date;
		CoverageEndDate = coverageEndDate.Date;
		DemandDate = demandDate.Date;
		DueDate = dueDate.Date;
		AllocationPercentage = allocationPercentage;
		DemandEnabled = demandEnabled;
		ReminderEnabled = reminderEnabled;
		ReminderDate = reminderDate?.Date;
		AllowPartialOverride = allowPartialOverride;
		GracePeriodDaysOverride = gracePeriodDaysOverride;
	}

	/// <summary>
	/// Updates editable installment fields. Only valid while the parent version is Draft.
	/// The AppService/Manager is responsible for enforcing draft-only mutation.
	/// </summary>
	public void Update(
		int sequence,
		string name,
		DateTime coverageStartDate,
		DateTime coverageEndDate,
		DateTime demandDate,
		DateTime dueDate,
		decimal allocationPercentage,
		bool demandEnabled,
		bool reminderEnabled,
		DateTime? reminderDate = null,
		bool? allowPartialOverride = null,
		int? gracePeriodDaysOverride = null)
	{
		Sequence = sequence;
		Name = name.Trim();
		CoverageStartDate = coverageStartDate.Date;
		CoverageEndDate = coverageEndDate.Date;
		DemandDate = demandDate.Date;
		DueDate = dueDate.Date;
		AllocationPercentage = allocationPercentage;
		DemandEnabled = demandEnabled;
		ReminderEnabled = reminderEnabled;
		ReminderDate = reminderDate?.Date;
		AllowPartialOverride = allowPartialOverride;
		GracePeriodDaysOverride = gracePeriodDaysOverride;
	}
}
