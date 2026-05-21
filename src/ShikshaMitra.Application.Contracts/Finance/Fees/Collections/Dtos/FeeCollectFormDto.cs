using ShikshaMitra.Finance.FeeCollection;
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

    /// <summary>Approved charges that have not yet been fully paid.</summary>
    public List<ChargeDetailDto> Charges { get; set; } = [];
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

    /// <summary>Original fee amount for this month.</summary>
    public double Total { get; set; }

    /// <summary>Amount already paid against this obligation.</summary>
    public double Paid { get; set; }

    /// <summary>Outstanding amount (Total − Paid, clamped to zero).</summary>
    public double Due { get; set; }

    public FeeCollectionStatus Status { get; set; }
}

/// <summary>Query parameters for the collect-form endpoint.</summary>
public class GetCollectFormInput
{
    public Guid StudentId { get; set; }
    public Guid AcademicYearId { get; set; }
}
