using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.Permissions.PermissionDefinitionProviders.Base;
using System.Collections.Generic;

namespace ShikshaMitra.Security.Permissions.PermissionDefinitionProviders;

/// <summary>
/// Permission definitions for Communication module.
/// Automatically reads configuration from ShikshaMitraSubmodulePermissions.Communication.
/// This provider creates a separate "Communication" permission group.
/// </summary>
public class CommunicationPermissionDefinitionProvider : BaseModulePermissionDefinitionProvider
{
	/// <summary>
	/// Gets the permission group name for Communication module.
	/// </summary>
	protected override string ModuleGroupName => ShikshaMitraSubmodulePermissions.Communication.GroupName;

	/// <summary>
	/// Gets the display name for Communication module permission group.
	/// </summary>
	protected override string ModuleGroupDisplayName => ShikshaMitraSubmodulePermissions.Communication.GroupDisplayName;

	/// <summary>
	/// Gets the root permission name for Communication module.
	/// </summary>
	protected override string ModulePermissionName => ShikshaMitraSubmodulePermissions.Communication.Default;

	/// <summary>
	/// Gets the display names for Communication module permissions.
	/// </summary>
	protected override Dictionary<string, string> ModuleDisplayNames => ShikshaMitraSubmodulePermissions.Communication.DisplayNames;

	/// <summary>
	/// Gets all submodules for Communication module (Notification, Settings).
	/// </summary>
	protected override Dictionary<string, ShikshaMitraSubmodulePermissions.SubmoduleInfo> ModuleSubmodules => ShikshaMitraSubmodulePermissions.Communication.Submodules;
}
