using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Application.Contracts.HR.Calendar.Events;

/// <summary>
/// DTO for creating and updating events
/// </summary>
public class CreateUpdateEventDto : IValidatableObject
{
    /// <summary>
    /// Academic Year ID for filtering events by academic year (required)
    /// UUID format: 550e8400-e29b-41d4-a716-446655440000
    /// </summary>
    [Required(ErrorMessage = "Academic year ID is required")]
    public Guid AcademicYearId { get; set; }

    /// <summary>
    /// Title of the event (1-256 characters, required)
    /// </summary>
    [Required(ErrorMessage = "Event title is required")]
    [StringLength(256, MinimumLength = 1, ErrorMessage = "Event title must be between 1 and 256 characters")]
    public string Title { get; set; }

    /// <summary>
    /// Reference to the EventType, required
    /// </summary>
    [Required(ErrorMessage = "Event type ID is required")]
    public Guid EventTypeId { get; set; }

    /// <summary>
    /// Optional description (0-500 characters)
    /// </summary>
    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string? Description { get; set; }

    /// <summary>
    /// Optional poster URL
    /// </summary>
    public string? Poster { get; set; }

    /// <summary>
    /// Event mode: 0=Online, 1=Offline
    /// </summary>
    public int Mode { get; set; } = 1; // Default to Offline

    /// <summary>
    /// Venue location (required for offline events)
    /// </summary>
    public string? Venue { get; set; }

    /// <summary>
    /// Meeting date, required
    /// </summary>
    [Required(ErrorMessage = "Meeting date is required")]
    public DateOnly MeetingDate { get; set; }

    /// <summary>
    /// Start time in HH:mm format, required
    /// </summary>
    [Required(ErrorMessage = "Start time is required")]
    [RegularExpression(@"^([0-1]?[0-9]|2[0-3]):[0-5][0-9]$", ErrorMessage = "Start time must be in HH:mm format")]
    public string StartTime { get; set; }

    /// <summary>
    /// End time in HH:mm format, required
    /// </summary>
    [Required(ErrorMessage = "End time is required")]
    [RegularExpression(@"^([0-1]?[0-9]|2[0-3]):[0-5][0-9]$", ErrorMessage = "End time must be in HH:mm format")]
    public string EndTime { get; set; }

    /// <summary>
    /// Whether event is recurring
    /// </summary>
    public bool IsRecurring { get; set; }

    /// <summary>
    /// Recurrence configuration (JSON)
    /// </summary>
    public string? RecurrenceJson { get; set; }

    /// <summary>
    /// List of user/group IDs this event applies to
    /// </summary>
    public List<Guid> AudienceIds { get; set; } = new();

    /// <summary>
    /// Validate times and cross-field constraints
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Time format validation is done via RegularExpression attributes
        // Additional custom validations can be added here
        yield break;
    }
}
