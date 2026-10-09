using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Security.Claims;

namespace ShikshaMitra.Security.Permissions;

public class UserPermissionDenyValueProvider(IPermissionStore permissionStore, IUserPermissionDenyStore denies)
    : PermissionValueProvider(permissionStore)
{
    public override string Name => "UserDeny";

    public override async Task<PermissionGrantResult> CheckAsync(PermissionValueCheckContext context) =>
        TryGetUserId(context.Principal, out var userId) && (await denies.GetDeniedAsync(userId)).Contains(context.Permission.Name)
            ? PermissionGrantResult.Prohibited : PermissionGrantResult.Undefined;

    public override async Task<MultiplePermissionGrantResult> CheckAsync(PermissionValuesCheckContext context)
    {
        var result = new MultiplePermissionGrantResult(context.Permissions.Select(permission => permission.Name).ToArray());
        if (!TryGetUserId(context.Principal, out var userId)) return result;
        var denied = await denies.GetDeniedAsync(userId);
        foreach (var name in result.Result.Keys.ToArray())
            if (denied.Contains(name)) result.Result[name] = PermissionGrantResult.Prohibited;
        return result;
    }

    private static bool TryGetUserId(ClaimsPrincipal? principal, out Guid userId) =>
        Guid.TryParse(principal?.FindFirst(AbpClaimTypes.UserId)?.Value ?? principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out userId);
}
