using ShikshaMitra.Common.Validation;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.Calendar.EventTypes;

/// <summary>
/// DTO for creating and updating event types
/// </summary>
public class CreateUpdateEventTypeDto
{
    /// <summary>
    /// Name of the event type (required, max 256 characters)
    /// </summary>
    [Required(ErrorMessage = "Event type name is required")]
    [StringLength(256, MinimumLength = 1, ErrorMessage = "Event type name must be between 1 and 256 characters")]
    public string Name { get; set; }

    /// <summary>
    /// Hex color code for UI representation (required, must be valid hex format)
    /// </summary>
    [Required(ErrorMessage = "Color is required")]
    [HexColor(ErrorMessage = "Color must be a valid hex code in format #RRGGBB or #RGB")]
    public string Color { get; set; }

    /// <summary>
    /// Optional description of the event type (max 500 characters)
    /// </summary>
    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string? Description { get; set; }
}
