using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Application.Contracts.Communication.AnnouncementTypes;

/// <summary>
/// DTO for announcement type details
/// </summary>
public class AnnouncementTypeDto : EntityDto<Guid>
{
    /// <summary>
    /// Name of the announcement type
    /// </summary>
    public string AnnouncementType { get; set; } = string.Empty;

    /// <summary>
    /// Description of the announcement type
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Color code for visual representation
    /// </summary>
    public string? Color { get; set; }
}
