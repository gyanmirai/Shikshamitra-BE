using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Application.Contracts.HR.Calendar.Events;

/// <summary>
/// DTO for getting paginated list of events with filters
/// </summary>
public class GetEventListRequestDto : PagedAndSortedResultRequestDto
{
    /// <summary>
    /// Academic Year ID for filtering events by academic year
    /// UUID string format (required for filtering)
    /// Example: 550e8400-e29b-41d4-a716-446655440000
    /// </summary>
    public Guid? AcademicYearId { get; set; }

    /// <summary>
    /// Search by title (case-insensitive substring match)
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// Filter by event type ID
    /// </summary>
    public Guid? TypeId { get; set; }

    /// <summary>
    /// Filter by event mode (0=Online, 1=Offline)
    /// </summary>
    public int? Mode { get; set; }

    /// <summary>
    /// Filter by status (0=Upcoming, 1=Active, 2=Completed)
    /// </summary>
    public int? Status { get; set; }

    /// <summary>
    /// Filter from date
    /// </summary>
    public DateTimeOffset? FromDate { get; set; }

    /// <summary>
    /// Filter to date
    /// </summary>
    public DateTimeOffset? ToDate { get; set; }
}
