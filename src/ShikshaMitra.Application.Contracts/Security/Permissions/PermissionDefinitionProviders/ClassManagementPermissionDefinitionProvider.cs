using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.Permissions.PermissionDefinitionProviders.Base;
using System.Collections.Generic;

namespace ShikshaMitra.Security.Permissions.PermissionDefinitionProviders;

/// <summary>
/// Permission definitions for Class Management module.
/// Automatically reads configuration from ShikshaMitraSubmodulePermissions.ClassManagement.
/// This provider creates a separate "ShikshaMitra.ClassManagement" permission group.
/// </summary>
public class ClassManagementPermissionDefinitionProvider : BaseModulePermissionDefinitionProvider
{
	/// <summary>
	/// Gets the permission group name for Class Management module.
	/// Creates a separate group for better organization and security isolation.
	/// </summary>
	protected override string ModuleGroupName => ShikshaMitraSubmodulePermissions.ClassManagement.GroupName;

	/// <summary>
	/// Gets the display name for Class Management module permission group.
	/// </summary>
	protected override string ModuleGroupDisplayName => ShikshaMitraSubmodulePermissions.ClassManagement.GroupDisplayName;

	/// <summary>
	/// Gets the root permission name for Class Management module.
	/// </summary>
	protected override string ModulePermissionName => ShikshaMitraSubmodulePermissions.ClassManagement.Default;

	/// <summary>
	/// Gets the display names for Class Management module permissions.
	/// </summary>
	protected override Dictionary<string, string> ModuleDisplayNames => ShikshaMitraSubmodulePermissions.ClassManagement.DisplayNames;

	/// <summary>
	/// Gets all submodules for Class Management module (Classes, Sections).
	/// </summary>
	protected override Dictionary<string, ShikshaMitraSubmodulePermissions.SubmoduleInfo> ModuleSubmodules => ShikshaMitraSubmodulePermissions.ClassManagement.Submodules;
}