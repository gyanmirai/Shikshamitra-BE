using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.Permissions.PermissionDefinitionProviders.Base;
using System.Collections.Generic;

namespace ShikshaMitra.Security.Permissions.PermissionDefinitionProviders;

/// <summary>
/// Permission definitions for Academics module.
/// Automatically reads configuration from ShikshaMitraSubmodulePermissions.Academics.
/// This provider creates a separate "ShikshaMitra.Academics" permission group.
/// </summary>
public class AcademicsPermissionDefinitionProvider : BaseModulePermissionDefinitionProvider
{
	/// <summary>
	/// Gets the permission group name for Academics module.
	/// Creates a separate group for better organization and security isolation.
	/// </summary>
	protected override string ModuleGroupName => ShikshaMitraSubmodulePermissions.Academics.GroupName;

	/// <summary>
	/// Gets the display name for Academics module permission group.
	/// </summary>
	protected override string ModuleGroupDisplayName => ShikshaMitraSubmodulePermissions.Academics.GroupDisplayName;

	/// <summary>
	/// Gets the root permission name for Academics module.
	/// </summary>
	protected override string ModulePermissionName => ShikshaMitraSubmodulePermissions.Academics.Default;

	/// <summary>
	/// Gets the display names for Academics module permissions.
	/// </summary>
	protected override Dictionary<string, string> ModuleDisplayNames => ShikshaMitraSubmodulePermissions.Academics.DisplayNames;

	/// <summary>
	/// Gets all submodules for Academics module (Curriculum, Exams, Timetable, Assignments).
	/// </summary>
	protected override Dictionary<string, ShikshaMitraSubmodulePermissions.SubmoduleInfo> ModuleSubmodules => ShikshaMitraSubmodulePermissions.Academics.Submodules;
}