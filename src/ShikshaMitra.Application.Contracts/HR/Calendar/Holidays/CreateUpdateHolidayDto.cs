using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Application.Contracts.HR.Calendar.Holidays;

/// <summary>
/// DTO for creating and updating holidays
/// </summary>
public class CreateUpdateHolidayDto : IValidatableObject
{
    /// <summary>
    /// Name of the holiday (1-256 characters, required)
    /// </summary>
    [Required(ErrorMessage = "Holiday name is required")]
    [StringLength(256, MinimumLength = 1, ErrorMessage = "Holiday name must be between 1 and 256 characters")]
    public string HolidayName { get; set; }

    /// <summary>
    /// Reference to the EventType, required
    /// </summary>
    [Required(ErrorMessage = "Holiday type ID is required")]
    public Guid HolidayTypeId { get; set; }

    /// <summary>
    /// Start date of the holiday, required
    /// </summary>
    [Required(ErrorMessage = "Start date is required")]
    public DateTimeOffset StartDate { get; set; }

    /// <summary>
    /// End date of the holiday (must be >= StartDate), required
    /// </summary>
    [Required(ErrorMessage = "End date is required")]
    public DateTimeOffset EndDate { get; set; }

    /// <summary>
    /// Optional description (0-500 characters)
    /// </summary>
    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string? Description { get; set; }

    /// <summary>
    /// Comma-separated audience IDs (users/groups) this holiday applies to
    /// </summary>
    public string? AudienceIds { get; set; }

    /// <summary>
    /// Comma-separated attachment file paths or URLs
    /// </summary>
    public string? Attachments { get; set; }

    /// <summary>
    /// Validate that end date is not before start date
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartDate > EndDate)
        {
            yield return new ValidationResult(
                "End date must be greater than or equal to start date",
                new[] { nameof(EndDate) });
        }
    }
}
