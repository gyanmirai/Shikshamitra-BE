using ShikshaMitra.TenantSettings.Reports;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Security.Permissions.PermissionDefinitionProviders;

public class InstituteReportPermissionDefinitionProvider : PermissionDefinitionProvider
{
	public override void Define(IPermissionDefinitionContext context)
	{
		var group = context.GetGroupOrNull("InstituteSetup") ?? context.AddGroup("InstituteSetup");
		foreach (var (name, label) in new[]
		{
			(InstituteReportPermissions.Navigation, "Reports"),
			(InstituteReportPermissions.View, "Reports: View"),
			(InstituteReportPermissions.Add, "Reports: Generate")
		}) group.AddPermission(name, new FixedLocalizableString(label), MultiTenancySides.Tenant);
	}
}
