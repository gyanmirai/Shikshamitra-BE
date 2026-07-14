using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Timetable.Substitutions;

public class SubstitutionNotificationLog : CreationAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid SubstitutionRequestId { get; private set; }
    public string EventName { get; private set; } = string.Empty;
    public string RecipientName { get; private set; } = string.Empty;
    public string Channel { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public string? Details { get; private set; }

    private SubstitutionNotificationLog()
    {
    }

    public SubstitutionNotificationLog(
        Guid id,
        Guid? tenantId,
        Guid substitutionRequestId,
        string eventName,
        string recipientName,
        string channel,
        string status,
        string? details = null)
        : base(id)
    {
        TenantId = tenantId;
        SubstitutionRequestId = substitutionRequestId;
        EventName = eventName;
        RecipientName = recipientName;
        Channel = channel;
        Status = status;
        Details = details;
    }
}
