using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Communication.AnnouncementTypes;

/// <summary>
/// Represents an announcement type/category for communication module
/// </summary>
public class AnnouncementType : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>
    /// Academic Medium ID associated with this announcement type
    /// </summary>
    public Guid? AcademicMediumId { get; set; }

    /// <summary>
    /// Name of the announcement type (e.g., "Holiday", "Event", "Notice")
    /// </summary>
    public string AnnouncementTypeName { get; set; } = string.Empty;

    /// <summary>
    /// Description of the announcement type
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Color code for visual representation (e.g., "#FF5733")
    /// </summary>
    public string? Color { get; set; }

    protected AnnouncementType()
    {
    }

    public AnnouncementType(
        Guid id,
        Guid? tenantId,
        Guid? academicMediumId,
        string announcementTypeName,
        string? description = null,
        string? color = null)
        : base(id)
    {
        TenantId = tenantId;
        AcademicMediumId = academicMediumId;
        AnnouncementTypeName = announcementTypeName;
        Description = description;
        Color = color;
    }

    public void Update(
        string announcementTypeName,
        string? description = null,
        string? color = null)
    {
        AnnouncementTypeName = announcementTypeName;
        Description = description;
        Color = color;
    }
}
