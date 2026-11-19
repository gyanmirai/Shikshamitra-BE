using ShikshaMitra.TenantSettings.SchoolDocuments;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Security.Permissions.PermissionDefinitionProviders;

public class SchoolDocumentPermissionDefinitionProvider : PermissionDefinitionProvider
{
	public override void Define(IPermissionDefinitionContext context)
	{
		var group = context.GetGroupOrNull("InstituteSetup") ?? context.AddGroup("InstituteSetup");
		foreach (var (name, label) in new[]
		{
			(SchoolDocumentPermissions.View, "School Documents: View"),
			(SchoolDocumentPermissions.Add, "School Documents: Add/Update"),
			(SchoolDocumentPermissions.Delete, "School Documents: Delete"),
			(SchoolDocumentPermissions.Navigation, "School Documents")
		})
			group.AddPermission(name, new FixedLocalizableString(label), MultiTenancySides.Tenant);
	}
}
