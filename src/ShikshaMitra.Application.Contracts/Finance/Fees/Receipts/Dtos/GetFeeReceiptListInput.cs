using System;
using ShikshaMitra.Finance.Common;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Finance.Fees.Receipts.Dtos;

/// <summary>
/// Input for the paginated receipt list API.
/// Board + Medium are required; Month and ReceiptKind are optional.
/// </summary>
public class GetFeeReceiptListInput : PagedAndSortedResultRequestDto, IFinanceClassDivisionFilter
{
    // ── Required context ──────────────────────────────────────────────────────

    public Guid AcademicYearId { get; set; }
    public Guid BoardId { get; set; }
    public Guid EducationMediumId { get; set; }

    // ── Optional filters ──────────────────────────────────────────────────────

    /// <summary>Month (1–12). When null, all months are returned.</summary>
    public int? Month { get; set; }

    /// <summary>Filter to a specific receipt kind. Null returns all kinds.</summary>
    public FeeReceiptKind? ReceiptKind { get; set; }

    /// <summary>Free-text search over receipt number, student name, or GR number.</summary>
    public string? Search { get; set; }

    public Guid? StandardId { get; set; }

    public Guid? DivisionId { get; set; }
}
