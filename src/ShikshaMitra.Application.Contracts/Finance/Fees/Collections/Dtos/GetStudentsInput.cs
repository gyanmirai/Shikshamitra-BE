using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Finance.Fees.Collections.Dtos;

/// <summary>Query parameters for the fee-collection list endpoint.</summary>
public class GetStudentsInput
{
    /// <summary>Academic year to filter by (required).</summary>
    [Required]
    public Guid AcademicYearId { get; set; }

    /// <summary>Education board to filter by (required).</summary>
    [Required]
    public Guid BoardId { get; set; }

    /// <summary>Education medium to filter by (required).</summary>
    [Required]
    public Guid EducationMediumId { get; set; }

    /// <summary>Optional search term — matches student name or roll number (≥ 2 chars).</summary>
    [MinLength(2)]
    [MaxLength(100)]
    public string? Search { get; set; }

    /// <summary>1-based page number (default 1).</summary>
    [Range(1, 10_000)]
    public int Page { get; set; } = 1;

    /// <summary>Page size (default 20, max 100).</summary>
    [Range(10, 100)]
    public int PageSize { get; set; } = 20;

    /// <summary>Optional fee-status filter: 0 = Pending, 1 = Paid, 2 = Overdue.</summary>
    public FeeCollectionStatus? Status { get; set; }
}
