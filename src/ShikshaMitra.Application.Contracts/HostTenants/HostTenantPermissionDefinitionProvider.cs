using ShikshaMitra.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;

namespace ShikshaMitra.HostTenants;

public class HostTenantPermissionDefinitionProvider : PermissionDefinitionProvider
{
	public override void Define(IPermissionDefinitionContext context)
	{
		context.GetPermissionOrNull(TenantManagementPermissions.Tenants.Default)?.AddChild(
			HostTenantConsts.ManageStatusPermission,
			LocalizableString.Create<ShikshaMitraResource>("Permission:Tenants.ManageStatus"),
			multiTenancySide: MultiTenancySides.Host);
	}
}
