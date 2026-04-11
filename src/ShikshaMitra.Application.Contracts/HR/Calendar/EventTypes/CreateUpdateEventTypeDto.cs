using ShikshaMitra.Common.Validation;

namespace ShikshaMitra.HR.Calendar.EventTypes;

/// <summary>
/// DTO for creating and updating event types
/// </summary>
public class CreateUpdateEventTypeDto
{
    /// <summary>
    /// Name of the event type (required, max 256 characters)
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Hex color code for UI representation (required, must be valid hex format)
    /// </summary>
    [HexColor(ErrorMessage = "Color must be a valid hex code in format #RRGGBB or #RGB")]
    public string Color { get; set; }

    /// <summary>
    /// Optional description of the event type (max 500 characters)
    /// </summary>
    public string? Description { get; set; }
}
