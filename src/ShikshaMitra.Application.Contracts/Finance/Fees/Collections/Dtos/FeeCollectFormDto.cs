using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Fees.Collections.Dtos;

/// <summary>
/// Data for the collect-fee dialog: student context, monthly DueFee ledger, and approved charges.
/// The ledger contains one <see cref="FeeLedgerMonthDto"/> per calendar month that has at least
/// one fee-head obligation; each <see cref="FeeLedgerLineDto"/> carries the <c>DueFeeId</c>
/// required for the <c>DueFeeAllocationDto</c> when posting a collection.
/// </summary>
public class FeeCollectFormDto
{
    public StudentInfoDto Student { get; set; } = default!;
    public FeeSummaryDto Summary { get; set; } = default!;

    /// <summary>Month-wise breakdown of outstanding obligations, ordered chronologically.</summary>
    public List<FeeLedgerMonthDto> Ledger { get; set; } = [];

    /// <summary>
    /// Collectible charges. Includes approved Charge Management charges and system-generated
    /// late-fee charges. System-generated late-fee charges exist only in Fee Collection and
    /// use their DueFee id as both ChargeId and SourceId.
    /// </summary>
    public List<ChargeDetailDto> Charges { get; set; } = [];

    /// <summary>
    /// Resolved from the student's Collection Policy (Finance/Policies) — null when no policy
    /// resolves for this student's fee structure, in which case online collection is treated
    /// as allowed by default (matches collection-policy-flow.md §7.10 "No Collection Policy
    /// Attached"). The staff collect form and the payment-method selector both read this.
    /// </summary>
    public bool AllowOnlineCollection { get; set; } = true;
}

/// <summary>All fee obligations grouped under one calendar month.</summary>
public class FeeLedgerMonthDto
{
    /// <summary>Display-friendly month name, e.g. "April".</summary>
    public string Month { get; set; } = string.Empty;

    /// <summary>Calendar year, e.g. 2026.</summary>
    public int Year { get; set; }

    /// <summary>Sum of <see cref="FeeLedgerLineDto.Due"/> across all lines in this month.</summary>
    public double TotalDue { get; set; }

    public List<FeeLedgerLineDto> Items { get; set; } = [];
}

/// <summary>A single fee-head obligation within one calendar month.</summary>
public class FeeLedgerLineDto
{
    /// <summary>
    /// The <c>DueFee</c> aggregate root id.  Must be sent back in <c>DueFeeAllocationDto</c>
    /// when the student pays so the outstanding balance is updated in the database.
    /// </summary>
    public Guid DueFeeId { get; set; }

    public Guid FeeHeadId { get; set; }
    public string FeeHeadName { get; set; } = string.Empty;

    /// <summary>Payment Term installment label when available, otherwise month/year.</summary>
    public string InstallmentLabel { get; set; } = string.Empty;

    /// <summary>Authoritative due date from the Payment Term schedule, when available.</summary>
    public DateTime? DueDate { get; set; }

    /// <summary>Installment coverage start (Payment Term schedules only).</summary>
    public DateTime? CoverageStartDate { get; set; }

    /// <summary>Installment coverage end (Payment Term schedules only).</summary>
    public DateTime? CoverageEndDate { get; set; }

    /// <summary><c>PaymentTerm</c> or <c>Legacy</c>.</summary>
    public string ScheduleSource { get; set; } = "Legacy";

    /// <summary>Original fee amount for this month.</summary>
    public double Total { get; set; }

    /// <summary>Full scheduled amount before proration. Equals Total when no proration applied.</summary>
    public double ScheduledTotal { get; set; }

    public Guid? ProrationPolicyId { get; set; }
    public Guid? ProrationPolicyVersionId { get; set; }
    /// <summary>Name captured by the immutable policy version used for this obligation.</summary>
    public string? ProrationPolicyName { get; set; }
    public int? ProrationPolicyVersionNumber { get; set; }
    public decimal? ProrationFactor { get; set; }
    public string? ProrationRuleType { get; set; }
    public string? ProrationRoundingMode { get; set; }
    public DateTime? ProrationEligibleStartDate { get; set; }
    public DateTime? ProrationEligibleEndDate { get; set; }
    public int? ProrationCoverageDays { get; set; }
    public int? ProrationEligibleDays { get; set; }
    public int? ProrationChargedDays { get; set; }
    public string? ProrationExplanation { get; set; }

    /// <summary>Amount already paid (cleared) against this obligation.</summary>
    public double Paid { get; set; }

    /// <summary>Outstanding amount (Total − Paid, clamped to zero).</summary>
    public double Due { get; set; }

    /// <summary>
    /// Amount of <see cref="Paid"/> that is currently held in reconciliation
    /// (cheque/DD submitted but not yet cleared by the finance team).
    /// A sub-set of <see cref="Paid"/> — not an additional amount on top of it.
    /// </summary>
    public double ReconciliationAmount { get; set; }

	public double ConcessionAmount { get; set; }

    public double RefundAmount { get; set; }
    public string? RefundDescription { get; set; }

	public FeeCollectionStatus Status { get; set; }

	/// <summary>
	/// Legacy ledger field retained for contract compatibility. Late fees are now returned as
	/// system-generated entries in <see cref="FeeCollectFormDto.Charges"/>, so this value is zero.
	/// </summary>
	public double LateFeeAmount { get; set; }

	/// <summary>One-line breakdown from LateFeeCalculationResult.Explanation, e.g. "₹20/day × 15 days late = ₹300 (capped by maximumLateFeeAmount)". Empty when LateFeeAmount is 0.</summary>
	public string LateFeeExplanation { get; set; } = string.Empty;

	/// <summary>Sum of Applied waivers against this due line's late fee — shown so the cashier can see a waiver was already granted, not just a smaller number.</summary>
	public double LateFeeWaivedAmount { get; set; }

	/// <summary>Legacy ledger action flag. Waiver actions are exposed on the generated charge.</summary>
	public bool CanWaive { get; set; }
}

/// <summary>Query parameters for the collect-form endpoint.</summary>
public class GetCollectFormInput
{
    public Guid StudentId { get; set; }
    public Guid AcademicYearId { get; set; }
}
