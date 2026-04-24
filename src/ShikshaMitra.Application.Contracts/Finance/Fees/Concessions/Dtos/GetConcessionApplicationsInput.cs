using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Finance.Fees.Concessions.Dtos;

/// <summary>
/// Query parameters for <c>GET /api/concession-request/concession-applications</c>.
/// </summary>
public class GetConcessionApplicationsInput
{
    /// <summary>Board identifier — required to scope results to one academic board.</summary>
    [Required]
    public Guid BoardId { get; set; }

    /// <summary>Education-medium identifier (English, Gujarati, Hindi, …).</summary>
    [Required]
    public Guid EducationMediumId { get; set; }

    /// <summary>
    /// Filter by API-level status: 0 = Pending, 1 = Approved, 2 = Rejected, 3 = Draft.
    /// Defaults to Draft (3) when omitted.
    /// </summary>
    public int? Status { get; set; }

    /// <summary>Page number (1-based). Defaults to 1.</summary>
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    /// <summary>Items per page. Defaults to 20, maximum 100.</summary>
    [Range(1, 100)]
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// Search by student name, roll number, or email.
    /// Must be at least 2 characters when provided.
    /// </summary>
    [MinLength(2)]
    public string? Search { get; set; }
}
