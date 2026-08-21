using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Calendar;

public class AcademicCalendarAuditReviewNote : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid AuditLogId { get; private set; }
    public string Note { get; private set; } = string.Empty;
    public Guid CreatedByUserId { get; private set; }
    public string? CreatedByName { get; private set; }
    public string? CreatedByRole { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private AcademicCalendarAuditReviewNote()
    {
    }

    public AcademicCalendarAuditReviewNote(
        Guid id,
        Guid? tenantId,
        Guid auditLogId,
        Guid createdByUserId,
        string note,
        string? createdByName,
        string? createdByRole) : base(id)
    {
        TenantId = tenantId;
        AuditLogId = auditLogId;
        CreatedByUserId = createdByUserId;
        Note = note;
        CreatedByName = createdByName;
        CreatedByRole = createdByRole;
        CreatedAt = DateTime.UtcNow;
    }
}
