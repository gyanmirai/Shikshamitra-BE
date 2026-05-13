using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.Permissions.PermissionDefinitionProviders.Base;
using System.Collections.Generic;

namespace ShikshaMitra.Security.Permissions.PermissionDefinitionProviders;

/// <summary>
/// Permission definitions for Transport module.
/// Automatically reads configuration from ShikshaMitraSubmodulePermissions.Transport.
/// This provider creates a separate "Transport" permission group.
/// </summary>
public class TransportPermissionDefinitionProvider : BaseModulePermissionDefinitionProvider
{
	/// <summary>
	/// Gets the permission group name for Transport module.
	/// </summary>
	protected override string ModuleGroupName => ShikshaMitraSubmodulePermissions.Transport.GroupName;

	/// <summary>
	/// Gets the display name for Transport module permission group.
	/// </summary>
	protected override string ModuleGroupDisplayName => ShikshaMitraSubmodulePermissions.Transport.GroupDisplayName;

	/// <summary>
	/// Gets the root permission name for Transport module.
	/// </summary>
	protected override string ModulePermissionName => ShikshaMitraSubmodulePermissions.Transport.Default;

	/// <summary>
	/// Gets the display names for Transport module permissions.
	/// </summary>
	protected override Dictionary<string, string> ModuleDisplayNames => ShikshaMitraSubmodulePermissions.Transport.DisplayNames;

	/// <summary>
	/// Gets all submodules for Transport module (Settings).
	/// </summary>
	protected override Dictionary<string, ShikshaMitraSubmodulePermissions.SubmoduleInfo> ModuleSubmodules => ShikshaMitraSubmodulePermissions.Transport.Submodules;
}
