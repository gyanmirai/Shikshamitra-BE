using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Calendar.HolidayTypes;

/// <summary>
/// Holiday Type aggregate root - master data for different types of holidays
/// </summary>
public class HolidayType : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    /// <summary>
    /// The name of the holiday type (e.g., "National Holiday", "Festival", "Declared Holiday")
    /// </summary>
    public string Name { get; private set; } = null!;

    /// <summary>
    /// Hexadecimal color code for UI representation (e.g., "#FF0000")
    /// </summary>
    public string Color { get; private set; } = null!;

    /// <summary>
    /// Detailed description of the holiday type
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the ID of the tenant this entity belongs to.
    /// </summary>
    public Guid? TenantId { get; private set; }

    /// <summary>
    /// Private constructor for EF Core
    /// </summary>
    private HolidayType()
    {
    }

    /// <summary>
    /// Factory method to create a new HolidayType
    /// </summary>
    public static HolidayType Create(Guid id, string name, string color, string? description = null, Guid? tenantId = null)
    {
        return new HolidayType
        {
            Id = id,
            Name = name,
            Color = color,
            Description = description,
            TenantId = tenantId
        };
    }
}
