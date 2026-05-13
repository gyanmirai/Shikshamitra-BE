using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.Permissions.PermissionDefinitionProviders.Base;
using System.Collections.Generic;

namespace ShikshaMitra.Security.Permissions.PermissionDefinitionProviders;

/// <summary>
/// Permission definitions for Library module.
/// Automatically reads configuration from ShikshaMitraSubmodulePermissions.Library.
/// This provider creates a separate "Library" permission group.
/// </summary>
public class LibraryPermissionDefinitionProvider : BaseModulePermissionDefinitionProvider
{
	/// <summary>
	/// Gets the permission group name for Library module.
	/// </summary>
	protected override string ModuleGroupName => ShikshaMitraSubmodulePermissions.Library.GroupName;

	/// <summary>
	/// Gets the display name for Library module permission group.
	/// </summary>
	protected override string ModuleGroupDisplayName => ShikshaMitraSubmodulePermissions.Library.GroupDisplayName;

	/// <summary>
	/// Gets the root permission name for Library module.
	/// </summary>
	protected override string ModulePermissionName => ShikshaMitraSubmodulePermissions.Library.Default;

	/// <summary>
	/// Gets the display names for Library module permissions.
	/// </summary>
	protected override Dictionary<string, string> ModuleDisplayNames => ShikshaMitraSubmodulePermissions.Library.DisplayNames;

	/// <summary>
	/// Gets all submodules for Library module (Settings).
	/// </summary>
	protected override Dictionary<string, ShikshaMitraSubmodulePermissions.SubmoduleInfo> ModuleSubmodules => ShikshaMitraSubmodulePermissions.Library.Submodules;
}
