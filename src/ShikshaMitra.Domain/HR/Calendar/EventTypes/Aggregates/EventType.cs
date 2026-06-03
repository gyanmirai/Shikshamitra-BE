using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Calendar.EventTypes;

/// <summary>
/// Represents an event type for calendar management.
/// Tenant-specific, supporting different event types per tenant.
/// </summary>
public class EventType : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }

    /// <summary>
    /// Name of the event type (e.g., "Meeting", "Holiday", "Conference")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Hex color code for UI representation (#RRGGBB format)
    /// </summary>
    public string Color { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of the event type
    /// </summary>
    public string? Description { get; set; }

    protected EventType() { }

    internal EventType(Guid id, Guid? tenantId, string name, string color, string? description = null) : base(id)
    {
        TenantId = tenantId;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Color = color ?? throw new ArgumentNullException(nameof(color));
        Description = description;
    }

    /// <summary>
    /// Factory method to create new EventType instance
    /// </summary>
    public static EventType Create(Guid? tenantId, string name, string color, string? description = null)
    {
        return new EventType(Guid.NewGuid(), tenantId, name, color, description);
    }

    /// <summary>
    /// Update event type details
    /// </summary>
    public void Update(string? name = null, string? color = null, string? description = null)
    {
        if (!string.IsNullOrWhiteSpace(name))
            Name = name;

        if (!string.IsNullOrWhiteSpace(color))
            Color = color;

        if (description != null)
            Description = description;
    }
}
