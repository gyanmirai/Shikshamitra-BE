using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Application.Contracts.HR.Calendar.Holidays;

/// <summary>
/// Request parameters for getting list of holidays with filtering
/// </summary>
public class GetHolidayListRequestDto : PagedAndSortedResultRequestDto
{
    /// <summary>
    /// Academic Year ID for filtering holidays by academic year
    /// UUID string format
    /// Example: 550e8400-e29b-41d4-a716-446655440000
    /// </summary>
    public Guid? AcademicYearId { get; set; }

    /// <summary>
    /// Search by holiday name (case-insensitive)
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// Filter by holiday type ID
    /// </summary>
    public Guid? TypeId { get; set; }

    /// <summary>
    /// Filter by status (0=Upcoming, 1=Ongoing, 2=Completed)
    /// </summary>
    public int? Status { get; set; }

    /// <summary>
    /// Filter holidays starting on or after this date
    /// </summary>
    public DateTimeOffset? FromDate { get; set; }

    /// <summary>
    /// Filter holidays ending on or before this date
    /// </summary>
    public DateTimeOffset? ToDate { get; set; }
}
