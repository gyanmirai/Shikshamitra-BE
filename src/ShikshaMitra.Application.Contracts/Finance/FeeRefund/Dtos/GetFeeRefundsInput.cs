using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Finance.FeeRefund.Dtos;

/// <summary>Paged + filtered input for the refund list endpoint.</summary>
public class GetFeeRefundsInput : PagedAndSortedResultRequestDto
{
    public Guid? StudentId { get; set; }

    /// <summary>
    /// Filters refund requests to students enrolled in a specific academic medium.
    /// Resolved server-side via a student sub-query on <c>Student.AcademicMediumId</c>.
    /// </summary>
    public Guid? AcademicMediumId { get; set; }

    public FeeRefundStatus? Status { get; set; }

    /// <summary>Free-text search on student name, GR number, or voucher number.</summary>
    public string? Search { get; set; }
}
