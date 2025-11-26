using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.Permissions.PermissionDefinitionProviders.Base;
using System.Collections.Generic;

namespace ShikshaMitra.Security.Permissions.PermissionDefinitionProviders;

/// <summary>
/// Permission definitions for HR module.
/// Automatically reads configuration from ShikshaMitraSubmodulePermissions.HR.
/// This provider creates a separate "ShikshaMitra.HR" permission group.
/// </summary>
public class HRPermissionDefinitionProvider : BaseModulePermissionDefinitionProvider
{
	/// <summary>
	/// Gets the permission group name for HR module.
	/// Creates a separate group for better organization and security isolation.
	/// </summary>
	protected override string ModuleGroupName => ShikshaMitraSubmodulePermissions.HR.GroupName;

	/// <summary>
	/// Gets the display name for HR module permission group.
	/// </summary>
	protected override string ModuleGroupDisplayName => ShikshaMitraSubmodulePermissions.HR.GroupDisplayName;

	/// <summary>
	/// Gets the root permission name for HR module.
	/// </summary>
	protected override string ModulePermissionName => ShikshaMitraSubmodulePermissions.HR.Default;

	/// <summary>
	/// Gets the display names for HR module permissions.
	/// </summary>
	protected override Dictionary<string, string> ModuleDisplayNames => ShikshaMitraSubmodulePermissions.HR.DisplayNames;

	/// <summary>
	/// Gets all submodules for HR module (EmployeeManagement, Attendance, Payroll, LeaveManagement).
	/// </summary>
	protected override Dictionary<string, ShikshaMitraSubmodulePermissions.SubmoduleInfo> ModuleSubmodules => ShikshaMitraSubmodulePermissions.HR.Submodules;
}