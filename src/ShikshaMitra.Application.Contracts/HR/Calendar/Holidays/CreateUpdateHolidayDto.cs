using System;
using System.Collections.Generic;

namespace ShikshaMitra.Application.Contracts.HR.Calendar.Holidays;

/// <summary>
/// DTO for creating and updating holidays
/// </summary>
public class CreateUpdateHolidayDto
{
    /// <summary>
    /// Academic Year ID for filtering holidays by academic year (required)
    /// UUID format: 550e8400-e29b-41d4-a716-446655440000
    /// </summary>
    public Guid AcademicYearId { get; set; }

    /// <summary>
    /// Name of the holiday (1-256 characters, required)
    /// </summary>
    public string HolidayName { get; set; } = string.Empty;

    /// <summary>
    /// Reference to the EventType, required
    /// </summary>
    public Guid HolidayTypeId { get; set; }

    /// <summary>
    /// Start date of the holiday, required
    /// </summary>
    public DateTimeOffset StartDate { get; set; }

    /// <summary>
    /// End date of the holiday (must be >= StartDate), required
    /// </summary>
    public DateTimeOffset EndDate { get; set; }

    /// <summary>
    /// Optional description (0-500 characters)
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// List of user/group IDs this holiday applies to
    /// </summary>
    public List<Guid> AudienceIds { get; set; } = new();

    /// <summary>
    /// Comma-separated attachment file paths or URLs
    /// </summary>
    public string? Attachments { get; set; }

}
