namespace ShikshaMitra.HR.Calendar;

/// <summary>
/// DTO for creating or updating a HolidayType
/// </summary>
public class CreateUpdateHolidayTypeDto
{
    /// <summary>
    /// Name of the holiday type (e.g., "National Holiday", "Festival")
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Hexadecimal color code for UI representation (#RRGGBB format)
    /// </summary>
    public string Color { get; set; }

    /// <summary>
    /// Optional description of the holiday type
    /// </summary>
    public string? Description { get; set; }
}
