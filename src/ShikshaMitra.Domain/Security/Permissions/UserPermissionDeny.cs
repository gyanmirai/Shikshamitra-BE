using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Security.Permissions;

public class UserPermissionDeny : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public string PermissionName { get; private set; } = null!;

    protected UserPermissionDeny() { }

    public UserPermissionDeny(Guid id, Guid? tenantId, Guid userId, string permissionName) : base(id)
    {
        TenantId = tenantId;
        UserId = userId;
        PermissionName = Check.NotNullOrWhiteSpace(permissionName, nameof(permissionName), 256);
    }
}
