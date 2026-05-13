using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.Permissions.PermissionDefinitionProviders.Base;
using System.Collections.Generic;

namespace ShikshaMitra.Security.Permissions.PermissionDefinitionProviders;

/// <summary>
/// Permission definitions for Calendar module.
/// Automatically reads configuration from ShikshaMitraSubmodulePermissions.Calendar.
/// This provider creates a separate "Calendar" permission group.
/// </summary>
public class CalendarPermissionDefinitionProvider : BaseModulePermissionDefinitionProvider
{
	/// <summary>
	/// Gets the permission group name for Calendar module.
	/// </summary>
	protected override string ModuleGroupName => ShikshaMitraSubmodulePermissions.Calendar.GroupName;

	/// <summary>
	/// Gets the display name for Calendar module permission group.
	/// </summary>
	protected override string ModuleGroupDisplayName => ShikshaMitraSubmodulePermissions.Calendar.GroupDisplayName;

	/// <summary>
	/// Gets the root permission name for Calendar module.
	/// </summary>
	protected override string ModulePermissionName => ShikshaMitraSubmodulePermissions.Calendar.Default;

	/// <summary>
	/// Gets the display names for Calendar module permissions.
	/// </summary>
	protected override Dictionary<string, string> ModuleDisplayNames => ShikshaMitraSubmodulePermissions.Calendar.DisplayNames;

	/// <summary>
	/// Gets all submodules for Calendar module (Settings).
	/// </summary>
	protected override Dictionary<string, ShikshaMitraSubmodulePermissions.SubmoduleInfo> ModuleSubmodules => ShikshaMitraSubmodulePermissions.Calendar.Submodules;
}
