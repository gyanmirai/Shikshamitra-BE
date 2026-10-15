using ShikshaMitra.TenantSettings.SchoolIdentity;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Security.Permissions.PermissionDefinitionProviders;

public class SchoolIdentityPermissionDefinitionProvider : PermissionDefinitionProvider
{
	public override void Define(IPermissionDefinitionContext context)
	{
		var group = context.GetGroupOrNull("InstituteSetup") ?? context.AddGroup("InstituteSetup");
		foreach (var (name, label) in new[]
		{
			(SchoolIdentityPermissions.View, "School Identity: View"),
			(SchoolIdentityPermissions.ManageDefaults, "School Identity: Manage School Defaults"),
			(SchoolIdentityPermissions.ManageOverrides, "School Identity: Manage Board and Medium Overrides"),
			(SchoolIdentityPermissions.AccessSignature, "School Identity: Access Principal Signatures")
		})
			group.AddPermission(name, new FixedLocalizableString(label), MultiTenancySides.Tenant);
		var navigation = context.GetPermissionOrNull(SchoolIdentityPermissions.Navigation);
		if (navigation == null)
			group.AddPermission(SchoolIdentityPermissions.Navigation, new FixedLocalizableString("School Identity"), MultiTenancySides.Tenant);
	}
}
