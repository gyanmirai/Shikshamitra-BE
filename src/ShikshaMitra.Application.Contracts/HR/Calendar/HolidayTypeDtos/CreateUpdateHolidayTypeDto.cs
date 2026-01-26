using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.Calendar;

/// <summary>
/// DTO for creating or updating a HolidayType
/// </summary>
public class CreateUpdateHolidayTypeDto
{
    /// <summary>
    /// Name of the holiday type (e.g., "National Holiday", "Festival")
    /// </summary>
    [Required]
    [StringLength(256)]
    public string Name { get; set; }

    /// <summary>
    /// Hexadecimal color code for UI representation (#RRGGBB format)
    /// </summary>
    [Required]
    [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Color must be in hex format (#RRGGBB)")]
    public string Color { get; set; }

    /// <summary>
    /// Optional description of the holiday type
    /// </summary>
    [StringLength(500)]
    public string? Description { get; set; }
}
