using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.Calendar.EventTypes;

/// <summary>
/// DTO for reading/displaying event type information
/// </summary>
public class EventTypeDto : AuditedEntityDto<Guid>
{
    /// <summary>
    /// Name of the event type
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Hex color code for UI representation
    /// </summary>
    public string Color { get; set; }

    /// <summary>
    /// Optional description of the event type
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Tenant ID (for multi-tenancy support)
    /// </summary>
    public Guid? TenantId { get; set; }
}
