using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.Permissions.PermissionDefinitionProviders.Base;
using System.Collections.Generic;

namespace ShikshaMitra.Security.Permissions.PermissionDefinitionProviders;

/// <summary>
/// Permission definitions for InstituteSetup module.
/// Automatically reads configuration from ShikshaMitraSubmodulePermissions.InstituteSetup.
/// This provider creates a separate "ShikshaMitra.InstituteSetup" permission group.
/// </summary>
public class InstituteSetupPermissionDefinitionProvider : BaseModulePermissionDefinitionProvider
{
	/// <summary>
	/// Gets the permission group name for InstituteSetup module.
	/// Creates a separate group for better organization and security isolation.
	/// </summary>
	protected override string ModuleGroupName => ShikshaMitraSubmodulePermissions.InstituteSetup.GroupName;

	/// <summary>
	/// Gets the display name for InstituteSetup module permission group.
	/// </summary>
	protected override string ModuleGroupDisplayName => ShikshaMitraSubmodulePermissions.InstituteSetup.GroupDisplayName;

	/// <summary>
	/// Gets the root permission name for InstituteSetup module.
	/// </summary>
	protected override string ModulePermissionName => ShikshaMitraSubmodulePermissions.InstituteSetup.Default;

	/// <summary>
	/// Gets the display names for InstituteSetup module permissions.
	/// </summary>
	protected override Dictionary<string, string> ModuleDisplayNames => ShikshaMitraSubmodulePermissions.InstituteSetup.DisplayNames;

	/// <summary>
	/// Gets all submodules for InstituteSetup module (BasicInformation, AcademicSetup, ShiftManagement, RoomsManagement).
	/// </summary>
	protected override Dictionary<string, ShikshaMitraSubmodulePermissions.SubmoduleInfo> ModuleSubmodules => ShikshaMitraSubmodulePermissions.InstituteSetup.Submodules;
}
