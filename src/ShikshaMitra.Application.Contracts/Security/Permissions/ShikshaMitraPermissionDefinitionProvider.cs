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

		var bookStoreGroup = context.AddGroup(
			ShikshaMitraPermissions.GroupName,
			L("Permission:BookStore")
		);

		var booksPermission = bookStoreGroup.AddPermission(
			ShikshaMitraPermissions.Books.Default,
			L("Permission:Books")
		);
		booksPermission.AddChild(
			ShikshaMitraPermissions.Books.Create,
			L("Permission:Books.Create")
		);
		booksPermission.AddChild(ShikshaMitraPermissions.Books.Edit, L("Permission:Books.Edit"));
		booksPermission.AddChild(
			ShikshaMitraPermissions.Books.Delete,
			L("Permission:Books.Delete")
		);

		var authorsPermission = bookStoreGroup.AddPermission(
			ShikshaMitraPermissions.Authors.Default,
			L("Permission:Authors")
		);
		authorsPermission.AddChild(
			ShikshaMitraPermissions.Authors.Create,
			L("Permission:Authors.Create")
		);
		authorsPermission.AddChild(
			ShikshaMitraPermissions.Authors.Edit,
			L("Permission:Authors.Edit")
		);
		authorsPermission.AddChild(
			ShikshaMitraPermissions.Authors.Delete,
			L("Permission:Authors.Delete")
		);
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
