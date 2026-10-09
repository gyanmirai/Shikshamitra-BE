using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Security.Permissions;

public interface IUserPermissionDenyStore
{
    Task<HashSet<string>> GetDeniedAsync(Guid userId);
    Task SetDeniedAsync(Guid userId, string permissionName, bool denied);
}

public class UserPermissionDenyStore(
    IRepository<UserPermissionDeny, Guid> repository,
    ICurrentTenant tenant,
    IGuidGenerator guids,
    IUnitOfWorkManager manager) : IUserPermissionDenyStore, ITransientDependency
{
    private const string CachePrefix = "UserPermissionDenies:";

    public async Task<HashSet<string>> GetDeniedAsync(Guid userId)
    {
        var key = $"{CachePrefix}{tenant.Id}:{userId}";
        var items = manager.Current?.Items;
        if (items != null && items.TryGetValue(key, out var cached))
            return (HashSet<string>)cached;
        var rows = await repository.GetListAsync(row => row.UserId == userId && row.TenantId == tenant.Id);
        var denied = rows.Select(row => row.PermissionName).ToHashSet(StringComparer.Ordinal);
        if (items != null) items[key] = denied;
        return denied;
    }

    public async Task SetDeniedAsync(Guid userId, string permissionName, bool denied)
    {
        var row = await repository.FindAsync(row => row.UserId == userId && row.TenantId == tenant.Id && row.PermissionName == permissionName);
        if (denied && row == null)
            await repository.InsertAsync(new UserPermissionDeny(guids.Create(), tenant.Id, userId, permissionName), autoSave: true);
        else if (!denied && row != null)
            await repository.DeleteAsync(row, autoSave: true);
        ClearLocalReuse(manager);
    }

    public static void ClearLocalReuse(IUnitOfWorkManager manager)
    {
        var items = manager.Current?.Items;
        if (items == null) return;
        foreach (var key in items.Keys.Where(key => key.StartsWith(CachePrefix, StringComparison.Ordinal)).ToArray())
            items.Remove(key);
    }
}
