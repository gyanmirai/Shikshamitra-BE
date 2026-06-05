using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Finance.FeeAdjustment;

/// <summary>
/// Allows fee-adjustment endpoints to honor legacy <c>Finance.Adjustment.*</c> grants
/// until tenant role migration completes.
/// </summary>
public class FeeAdjustmentLegacyPermissionAuthorizationHandler(IPermissionChecker permissionChecker)
    : AuthorizationHandler<PermissionRequirement>, ITransientDependency
{
    private static readonly Dictionary<string, string> LegacyPermissionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        [ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.View] =
            ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.LegacyView,
        [ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Add] =
            ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.LegacyAdd,
        [ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Delete] =
            ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.LegacyDelete,
        [ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Approve] =
            ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.LegacyAdd,
        [ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Reject] =
            ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.LegacyAdd,
    };

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (!LegacyPermissionMap.TryGetValue(requirement.PermissionName, out var legacyPermission))
        {
            return;
        }

        if (await permissionChecker.IsGrantedAsync(requirement.PermissionName) ||
            await permissionChecker.IsGrantedAsync(legacyPermission))
        {
            context.Succeed(requirement);
        }
    }
}
