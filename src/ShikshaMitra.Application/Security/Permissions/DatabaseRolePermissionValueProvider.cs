using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Security.Permissions;

public class DatabaseRolePermissionValueProvider(
	IPermissionStore permissionStore,
	AdminPermissionAccess admins,
	ICurrentTenant tenant) : PermissionValueProvider(permissionStore)
{
	public override string Name => RolePermissionValueProvider.ProviderName;

	public override async Task<PermissionGrantResult> CheckAsync(PermissionValueCheckContext context)
	{
		if (!context.Permission.MultiTenancySide.HasFlag(tenant.GetMultiTenancySide())) return PermissionGrantResult.Prohibited;
		if (!AdminPermissionAccess.TryGetUserId(context.Principal, out var userId)) return PermissionGrantResult.Undefined;
		var roles = await admins.GetRolesAsync(userId);
		if (roles == null) return PermissionGrantResult.Undefined;
		if (roles.Contains(AdminPermissionAccess.RoleName, StringComparer.OrdinalIgnoreCase)) return PermissionGrantResult.Granted;
		foreach (var role in roles)
			if (await PermissionStore.IsGrantedAsync(context.Permission.Name, Name, role)) return PermissionGrantResult.Granted;
		return PermissionGrantResult.Undefined;
	}

	public override async Task<MultiplePermissionGrantResult> CheckAsync(PermissionValuesCheckContext context)
	{
		var result = new MultiplePermissionGrantResult(context.Permissions.Select(permission => permission.Name).ToArray());
		foreach (var permission in context.Permissions.Where(permission => !permission.MultiTenancySide.HasFlag(tenant.GetMultiTenancySide())))
			result.Result[permission.Name] = PermissionGrantResult.Prohibited;
		var names = context.Permissions.Where(permission => permission.MultiTenancySide.HasFlag(tenant.GetMultiTenancySide()))
			.Select(permission => permission.Name).ToArray();
		if (names.Length == 0) return result;
		if (!AdminPermissionAccess.TryGetUserId(context.Principal, out var userId)) return result;
		var roles = await admins.GetRolesAsync(userId);
		if (roles == null) return result;
		if (roles.Contains(AdminPermissionAccess.RoleName, StringComparer.OrdinalIgnoreCase))
		{
			foreach (var name in names) result.Result[name] = PermissionGrantResult.Granted;
			return result;
		}
		foreach (var role in roles)
		{
			var grants = await PermissionStore.IsGrantedAsync(names, Name, role);
			foreach (var name in names)
				if (grants.Result[name] == PermissionGrantResult.Granted) result.Result[name] = PermissionGrantResult.Granted;
		}
		return result;
	}
}
