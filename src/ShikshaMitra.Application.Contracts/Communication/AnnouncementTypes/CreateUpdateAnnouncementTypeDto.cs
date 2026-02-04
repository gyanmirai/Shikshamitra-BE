using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Application.Contracts.Communication.AnnouncementTypes;

/// <summary>
/// DTO for creating or updating announcement type
/// </summary>
public class CreateUpdateAnnouncementTypeDto
{
    /// <summary>
    /// Name of the announcement type
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string AnnouncementType { get; set; } = string.Empty;

    /// <summary>
    /// Description of the announcement type
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Color code for visual representation (e.g., "#FF5733")
    /// </summary>
    [MaxLength(20)]
    public string? Color { get; set; }
}
