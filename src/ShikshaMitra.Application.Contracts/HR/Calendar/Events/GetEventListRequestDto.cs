using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Application.Contracts.HR.Calendar.Events;

/// <summary>
/// DTO for getting paginated list of events with filters
/// </summary>
public class GetEventListRequestDto : PagedAndSortedResultRequestDto
{
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
    public DateOnly? FromDate { get; set; }

    /// <summary>
    /// Filter to date
    /// </summary>
    public DateOnly? ToDate { get; set; }
}
