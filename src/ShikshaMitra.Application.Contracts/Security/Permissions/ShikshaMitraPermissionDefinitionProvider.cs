using Volo.Abp.Authorization.Permissions;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using static ShikshaMitra.Localization.LocalizationHelper;

namespace ShikshaMitra.Security.Permissions;

public class ShikshaMitraPermissionDefinitionProvider : PermissionDefinitionProvider
{
	public override void Define(IPermissionDefinitionContext context)
	{
		SetTenantPermissionMultiTenancySide(context);
	}

	private void SetTenantPermissionMultiTenancySide(IPermissionDefinitionContext context)
	{
		var permissionsToUpdate = new[]
		{
			TenantManagementPermissions.Tenants.Default,
			TenantManagementPermissions.Tenants.Update,
		};

		foreach (var permissionName in permissionsToUpdate)
		{
			var permission = context.GetPermissionOrNull(permissionName);
			if (permission != null)
			{
				permission.MultiTenancySide = MultiTenancySides.Both;
			}
			else
			{
				// Log or handle as needed, but avoid throwing to keep permission definition robust
				// Example: continue;
			}
		}
	}
}
