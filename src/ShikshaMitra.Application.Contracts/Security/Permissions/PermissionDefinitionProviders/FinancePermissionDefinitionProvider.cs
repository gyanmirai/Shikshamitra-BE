using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.Permissions.PermissionDefinitionProviders.Base;
using System.Collections.Generic;

namespace ShikshaMitra.Security.Permissions.PermissionDefinitionProviders;

/// <summary>
/// Permission definitions for Finance module.
/// Automatically reads configuration from ShikshaMitraSubmodulePermissions.Finance.
/// This provider creates a separate "ShikshaMitra.Finance" permission group.
/// </summary>
public class FinancePermissionDefinitionProvider : BaseModulePermissionDefinitionProvider
{
	/// <summary>
	/// Gets the permission group name for Finance module.
	/// Creates a separate group for better organization and security isolation.
	/// </summary>
	protected override string ModuleGroupName => ShikshaMitraSubmodulePermissions.Finance.GroupName;

	/// <summary>
	/// Gets the display name for Finance module permission group.
	/// </summary>
	protected override string ModuleGroupDisplayName => ShikshaMitraSubmodulePermissions.Finance.GroupDisplayName;

	/// <summary>
	/// Gets the root permission name for Finance module.
	/// </summary>
	protected override string ModulePermissionName => ShikshaMitraSubmodulePermissions.Finance.Default;

	/// <summary>
	/// Gets the display names for Finance module permissions.
	/// </summary>
	protected override Dictionary<string, string> ModuleDisplayNames => ShikshaMitraSubmodulePermissions.Finance.DisplayNames;

	/// <summary>
	/// Gets all submodules for Finance module (FeeManagement, FeeCollection, Expenses, Reports).
	/// </summary>
	protected override Dictionary<string, ShikshaMitraSubmodulePermissions.SubmoduleInfo> ModuleSubmodules => ShikshaMitraSubmodulePermissions.Finance.Submodules;
}