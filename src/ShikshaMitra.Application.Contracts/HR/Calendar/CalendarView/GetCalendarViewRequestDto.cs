using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Application.Contracts.HR.Calendar.CalendarView;

/// <summary>
/// Request DTO for getting calendar view data with filtering and pagination
/// </summary>
public class GetCalendarViewRequestDto : PagedAndSortedResultRequestDto
{
    /// <summary>
    /// Academic Year ID for filtering (required)
    /// </summary>
    public Guid AcademicYearId { get; set; }

    /// <summary>
    /// Filter by specific event type IDs (optional)
    /// </summary>
    public List<Guid>? EventTypeIds { get; set; }

    /// <summary>
    /// Filter by specific holiday type IDs (optional)
    /// </summary>
    public List<Guid>? HolidayTypeIds { get; set; }

    /// <summary>
    /// Filter by status: 0=Upcoming, 1=Active/Ongoing, 2=Completed (optional)
    /// </summary>
    public int? Status { get; set; }

    /// <summary>
    /// Filter by one or more statuses: 0=Upcoming, 1=Active/Ongoing, 2=Completed (optional)
    /// </summary>
    public List<int>? Statuses { get; set; }

    /// <summary>
    /// Search by title (optional)
    /// </summary>
    public string? Search { get; set; }
}
