using ShikshaMitra.Constants;
using ShikshaMitra.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;

namespace ShikshaMitra.Security.Permissions;

public class ShikshaMitraPermissionDefinitionProvider : PermissionDefinitionProvider
{
	public override void Define(IPermissionDefinitionContext context)
	{
		SetTenantPermissionMultiTenancySide(context);
		var group = context.AddGroup(ShikshaMitraPermissions.GroupName, L("Permission:ShikshaMitra"));

		// Define Module Visibility Permissions (these go in JWT)
		DefineModulePermissions(group);
		DefineSubmodulePermissions(group);
	}

	private static void DefineModulePermissions(PermissionGroupDefinition group)
	{
		// Use ShikshaMitraModulePermissions from the domain shared project
		foreach (var modulePermission in ShikshaMitraModulePermissions.All)
		{
			group.AddPermission(modulePermission, L(ShikshaMitraModulePermissions.DisplayNames[modulePermission]));
		}
	}

	private static void DefineSubmodulePermissions(PermissionGroupDefinition group)
	{
		// Use ShikshaMitraModulePermissions from the domain shared project
		foreach (var submodulePermission in ShikshaMitraSubModulePermissions.All)
		{
			group.AddPermission(submodulePermission, L(ShikshaMitraSubModulePermissions.DisplayNames[submodulePermission]));
		}
	}

	private static LocalizableString L(string name)
	{
		return LocalizableString.Create<ShikshaMitraResource>(name);
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
