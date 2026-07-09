using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Timetable.Substitutions;

public class SubstitutionAuditLog : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid SubstitutionRequestId { get; private set; }
    public DateTime Timestamp { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public string ActorName { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;
    public string? Details { get; private set; }

    private SubstitutionAuditLog()
    {
    }

    public SubstitutionAuditLog(
        Guid id,
        Guid? tenantId,
        Guid substitutionRequestId,
        string actorName,
        string action,
        string? details = null,
        Guid? actorUserId = null)
        : base(id)
    {
        TenantId = tenantId;
        SubstitutionRequestId = substitutionRequestId;
        ActorName = actorName;
        Action = action;
        Details = details;
        ActorUserId = actorUserId;
        Timestamp = DateTime.UtcNow;
    }
}
