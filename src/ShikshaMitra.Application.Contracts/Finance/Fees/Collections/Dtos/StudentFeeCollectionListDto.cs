using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Fees.Collections.Dtos;

/// <summary>Paginated response for the fee-collection list endpoint.</summary>
public class StudentFeeCollectionListDto
{
    /// <summary>Total number of records matching the filter criteria.</summary>
    public int TotalCount { get; set; }

    /// <summary>Current page number (1-indexed).</summary>
    public int CurrentPage { get; set; }

    /// <summary>Number of records per page.</summary>
    public int PageSize { get; set; }

    /// <summary>Total number of pages available.</summary>
    public int TotalPages { get; set; }

    /// <summary>Students with their fee collection status for the current page.</summary>
    public List<FeeCollectionListItemDto> Items { get; set; } = [];
}

/// <summary>Single student row in the fee-collection list.</summary>
public class FeeCollectionListItemDto
{
    /// <summary>
    /// Unique identifier of the <c>StudentFeeRecord</c> for this student+academic-year.
    /// <c>Guid.Empty</c> when no fee record has been created yet.
    /// </summary>
    public Guid StudentFeeId { get; set; }

    public Guid StudentId { get; set; }

    public string StudentName { get; set; } = string.Empty;

    public string StandardName { get; set; } = string.Empty;

    public string DivisionName { get; set; } = string.Empty;

    public string RollNo { get; set; } = string.Empty;

    public string GrNo { get; set; } = string.Empty;

    public double TotalFees { get; set; }

    public double PaidFees { get; set; }

    public double PendingFees { get; set; }

    /// <summary>Sum of unpaid charge amounts whose due date has already passed.</summary>
    public double OverdueFees { get; set; }

    /// <summary>0 = Pending, 1 = Paid, 2 = Overdue.</summary>
    public FeeCollectionStatus Status { get; set; }
}
