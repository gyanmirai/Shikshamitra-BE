using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Curriculum.Settings;

public class SubjectAuditLog : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid SubjectId { get; private set; }
    
    public string Action { get; private set; } 
    public string OldValueJson { get; private set; }
    public string NewValueJson { get; private set; }
    public string? Reason { get; private set; }

    public Guid ChangedByUserId { get; private set; }
    public DateTime ChangedAt { get; private set; }

    public string Module { get; private set; } = "SubjectCreation";
    public string? SubModule { get; private set; }
    public string? EntityType { get; private set; }
    public string? EntityName { get; private set; }

    public string? ChangedFieldsJson { get; private set; }
    public bool HasDiff { get; private set; }

    public string? ActorNameSnapshot { get; private set; }
    public string? ActorRoleSnapshot { get; private set; }
    public string? ActorEmailSnapshot { get; private set; }

    public string? Source { get; private set; }
    public string? IpAddress { get; private set; }
    public string? Device { get; private set; }
    public string? Browser { get; private set; }
    public string? OperatingSystem { get; private set; }
    public string? RequestPath { get; private set; }
    public string? HttpMethod { get; private set; }
    public string? CorrelationId { get; private set; }

    private SubjectAuditLog()
    {
        /* ORM */
    }

    public SubjectAuditLog(
        Guid id,
        Guid? tenantId,
        Guid subjectId,
        string action,
        string oldValueJson,
        string newValueJson,
        Guid changedByUserId,
        string? reason = null,
        string module = "SubjectCreation",
        string? subModule = null,
        string? entityType = null,
        string? entityName = null,
        string? changedFieldsJson = null,
        string? actorNameSnapshot = null,
        string? actorRoleSnapshot = null,
        string? actorEmailSnapshot = null,
        string? source = null,
        string? ipAddress = null,
        string? device = null,
        string? browser = null,
        string? operatingSystem = null,
        string? requestPath = null,
        string? httpMethod = null,
        string? correlationId = null)
        : base(id)
    {
        TenantId = tenantId;
        SubjectId = subjectId;
        Action = action;
        OldValueJson = oldValueJson;
        NewValueJson = newValueJson;
        ChangedByUserId = changedByUserId;
        Reason = reason;
        ChangedAt = DateTime.UtcNow;

        Module = module;
        SubModule = subModule;
        EntityType = entityType;
        EntityName = entityName;
        ChangedFieldsJson = changedFieldsJson;
        HasDiff = !string.IsNullOrWhiteSpace(changedFieldsJson) && changedFieldsJson != "[]";
        ActorNameSnapshot = actorNameSnapshot;
        ActorRoleSnapshot = actorRoleSnapshot;
        ActorEmailSnapshot = actorEmailSnapshot;
        Source = source;
        IpAddress = ipAddress;
        Device = device;
        Browser = browser;
        OperatingSystem = operatingSystem;
        RequestPath = requestPath;
        HttpMethod = httpMethod;
        CorrelationId = correlationId;
    }
}
