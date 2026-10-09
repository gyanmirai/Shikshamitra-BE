using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Uow;

namespace ShikshaMitra.Security.Permissions;

public partial class ShikshaMitraPermissionAppService
{
    public virtual async Task<UserPermissionOverridesDto> GetUserOverridesAsync(Guid userId)
    {
        var key = userId.ToString();
        var catalogue = await GetAsync("U", key);
        var result = new UserPermissionOverridesDto { EntityDisplayName = catalogue.EntityDisplayName, Groups = catalogue.Groups };
        var denied = await _denies.GetDeniedAsync(userId);
        foreach (var permission in catalogue.Groups.SelectMany(group => group.Permissions))
        {
            if (denied.Contains(permission.Name)) result.Overrides[permission.Name] = "deny";
            else if (permission.GrantedProviders.Any(provider => provider.ProviderName == "U" && provider.ProviderKey == key))
                result.Overrides[permission.Name] = "allow";
        }
        return result;
    }

    public virtual async Task UpdateUserOverridesAsync(Guid userId, UpdateUserPermissionOverridesDto input)
    {
        using var ownedTransaction = _unitOfWorkManager.Current?.Options.IsTransactional == true
            ? null : _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
        var transaction = ownedTransaction ?? _unitOfWorkManager.Current!;
        try
        {
            await CheckProviderPolicy("U", userId.ToString());
            if (input?.Permissions == null || input.Permissions.Any(item => item == null || item.State is not ("inherit" or "allow" or "deny")))
                throw InvalidPermissions();
            await UpdateAsync("U", userId.ToString(), new UpdatePermissionsDto
            {
                Permissions = input.Permissions.Select(item => new UpdatePermissionDto { Name = item.Name, IsGranted = item.State == "allow" }).ToArray()
            });
            foreach (var item in input.Permissions)
                await _denies.SetDeniedAsync(userId, item.Name, item.State == "deny");
            if (ownedTransaction != null) await ownedTransaction.CompleteAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
