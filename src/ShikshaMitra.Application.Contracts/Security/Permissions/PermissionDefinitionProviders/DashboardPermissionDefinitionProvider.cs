using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.Permissions.PermissionDefinitionProviders.Base;
using System.Collections.Generic;

namespace ShikshaMitra.Security.Permissions.PermissionDefinitionProviders;

/// <summary>
/// Permission definitions for Dashboard module.
/// Automatically reads configuration from ShikshaMitraSubmodulePermissions.Dashboard.
/// This provider creates a separate "ShikshaMitra.Dashboard" permission group.
/// </summary>
public class DashboardPermissionDefinitionProvider : BaseModulePermissionDefinitionProvider
{
	/// <summary>
	/// Gets the permission group name for Dashboard module.
	/// Creates a separate group for better organization and security isolation.
	/// </summary>
	protected override string ModuleGroupName => ShikshaMitraSubmodulePermissions.Dashboard.GroupName;

	/// <summary>
	/// Gets the display name for Dashboard module permission group.
	/// </summary>
	protected override string ModuleGroupDisplayName => ShikshaMitraSubmodulePermissions.Dashboard.GroupDisplayName;

	/// <summary>
	/// Gets the root permission name for Dashboard module.
	/// </summary>
	protected override string ModulePermissionName => ShikshaMitraSubmodulePermissions.Dashboard.Default;

	/// <summary>
	/// Gets the display names for Dashboard module permissions.
	/// </summary>
	protected override Dictionary<string, string> ModuleDisplayNames => ShikshaMitraSubmodulePermissions.Dashboard.DisplayNames;

	/// <summary>
	/// Gets all submodules for Dashboard module (Analytics, Reports).
	/// </summary>
	protected override Dictionary<string, ShikshaMitraSubmodulePermissions.SubmoduleInfo> ModuleSubmodules => ShikshaMitraSubmodulePermissions.Dashboard.Submodules;
}