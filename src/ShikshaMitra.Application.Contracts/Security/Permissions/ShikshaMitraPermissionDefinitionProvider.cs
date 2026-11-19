using ShikshaMitra.Constants.Permissions;
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
		var group = context.AddGroup(ShikshaMitraPermissions.GroupName, L("Permission:ShikshaMitra"));

		DefineUserScopePermissions(group);
		DefineGroupPermissions(group);
			SetTenantPermissionMultiTenancySide(context);

		// Define Module Visibility Permissions (these go in JWT)
		DefineModulePermissions(group);
		DefineSubmodulePermissions(group);

		DefineAbpIdentityCustomPermissions(context);
	}

	private void DefineAbpIdentityCustomPermissions(IPermissionDefinitionContext context)
	{
		var usersPermission = context.GetPermissionOrNull("AbpIdentity.Users");
		if (usersPermission != null)
		{
			if (context.GetPermissionOrNull("AbpIdentity.Users.Add") == null)
			{
				usersPermission.AddChild("AbpIdentity.Users.Add", L("Permission:Users.Add"));
			}
			if (context.GetPermissionOrNull("AbpIdentity.Users.View") == null)
			{
				usersPermission.AddChild("AbpIdentity.Users.View", L("Permission:Users.View"));
			}
		}

		var rolesPermission = context.GetPermissionOrNull("AbpIdentity.Roles");
		if (rolesPermission != null)
		{
			if (context.GetPermissionOrNull("AbpIdentity.Roles.Add") == null)
			{
				rolesPermission.AddChild("AbpIdentity.Roles.Add", L("Permission:Roles.Add"));
			}
			if (context.GetPermissionOrNull("AbpIdentity.Roles.View") == null)
			{
				rolesPermission.AddChild("AbpIdentity.Roles.View", L("Permission:Roles.View"));
			}
		}
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

	private void DefineUserScopePermissions(PermissionGroupDefinition group)
	{
		var userScopePermission = group.AddPermission(
			ShikshaMitraPermissions.UserScope.Default,
			displayName: L("Permission:UserScope")
		);

		userScopePermission.AddChild(
			ShikshaMitraPermissions.UserScope.View,
			displayName: L("Permission:UserScope.View")
		);

		userScopePermission.AddChild(
			ShikshaMitraPermissions.UserScope.Assign,
			displayName: L("Permission:UserScope.Assign")
		);

		userScopePermission.AddChild(
			ShikshaMitraPermissions.UserScope.BulkAssign,
			displayName: L("Permission:UserScope.BulkAssign")
		);

		userScopePermission.AddChild(
			ShikshaMitraPermissions.UserScope.Revoke,
			displayName: L("Permission:UserScope.Revoke")
		);

		userScopePermission.AddChild(
			ShikshaMitraPermissions.UserScope.Manage,
			displayName: L("Permission:UserScope.Manage")
		);
	}

	private void DefineGroupPermissions(PermissionGroupDefinition group)
	{
		var groupPermission = group.AddPermission(
			ShikshaMitraPermissions.Group.Default,
			displayName: L("Permission:Group")
		);

		groupPermission.AddChild(
			ShikshaMitraPermissions.Group.List,
			displayName: L("Permission:Group.List")
		);

		groupPermission.AddChild(
			ShikshaMitraPermissions.Group.View,
			displayName: L("Permission:Group.View")
		);

		groupPermission.AddChild(
			ShikshaMitraPermissions.Group.Add,
			displayName: L("Permission:Group.Add")
		);

		groupPermission.AddChild(
			// Keep the legacy permission identity registered alongside Add.
			ShikshaMitraPermissions.Group.Default + ".Create",
			displayName: L("Permission:Group.Create")
		);

		groupPermission.AddChild(
			ShikshaMitraPermissions.Group.Edit,
			displayName: L("Permission:Group.Edit")
		);

		groupPermission.AddChild(
			ShikshaMitraPermissions.Group.Delete,
			displayName: L("Permission:Group.Delete")
		);
	}

	private void SetTenantPermissionMultiTenancySide(IPermissionDefinitionContext context)
	{
		var permissionsToUpdate = new[]
		{
			"AbpPermissionManagement.Permissions",
			"AbpPermissionManagement.Permissions.Manage",
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
