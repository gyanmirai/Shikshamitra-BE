using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.Permissions.PermissionDefinitionProviders.Base;
using System.Collections.Generic;

namespace ShikshaMitra.Security.Permissions.PermissionDefinitionProviders;

/// <summary>
/// Permission definitions for Students module.
/// Automatically reads configuration from ShikshaMitraSubmodulePermissions.Students.
/// This provider creates a separate "ShikshaMitra.Students" permission group.
/// </summary>
public class StudentsPermissionDefinitionProvider : BaseModulePermissionDefinitionProvider
{
	/// <summary>
	/// Gets the permission group name for Students module.
	/// Creates a separate group for better organization and security isolation.
	/// </summary>
	protected override string ModuleGroupName => ShikshaMitraSubmodulePermissions.Students.GroupName;

	/// <summary>
	/// Gets the display name for Students module permission group.
	/// </summary>
	protected override string ModuleGroupDisplayName => ShikshaMitraSubmodulePermissions.Students.GroupDisplayName;

	/// <summary>
	/// Gets the root permission name for Students module.
	/// </summary>
	protected override string ModulePermissionName => ShikshaMitraSubmodulePermissions.Students.Default;

	/// <summary>
	/// Gets the display names for Students module permissions.
	/// </summary>
	protected override Dictionary<string, string> ModuleDisplayNames => ShikshaMitraSubmodulePermissions.Students.DisplayNames;

	/// <summary>
	/// Gets all submodules for Students module (StudentRecords, Admission, Attendance, Promotion).
	/// </summary>
	protected override Dictionary<string, ShikshaMitraSubmodulePermissions.SubmoduleInfo> ModuleSubmodules => ShikshaMitraSubmodulePermissions.Students.Submodules;
}