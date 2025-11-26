using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

/// <summary>
/// Central repository for all submodule permissions.
/// This class is split across multiple files using partial classes for better maintainability.
/// </summary>
/// <remarks>
/// File Organization:
/// - ShikshaMitraSubmodulePermissions.Base.cs (Core/Core definitions)
/// - ShikshaMitraSubmodulePermissions.Students.cs
/// - ShikshaMitraSubmodulePermissions.Academics.cs
/// - ShikshaMitraSubmodulePermissions.Finance.cs
/// - ShikshaMitraSubmodulePermissions.HR.cs
/// - ShikshaMitraSubmodulePermissions.Dashboard.cs
/// - ShikshaMitraSubmodulePermissions.ClassManagement.cs
/// - ShikshaMitraSubmodulePermissions.Registry.cs
/// </remarks>
public static partial class ShikshaMitraSubmodulePermissions
{
	/// <summary>
	/// Root group name prefix for all ShikshaMitra permissions.
	/// Each module creates its own group: ShikshaMitra.ModuleName
	/// </summary>
	public const string GroupName = "ShikshaMitra";
	
	/// <summary>
	/// Display name prefix for localization
	/// </summary>
	private const string DisplayNamePrefix = "Permission";

	/// <summary>
	/// Helper record to store submodule metadata
	/// </summary>
	/// <param name="Permissions">Array of all permission strings for the submodule</param>
	/// <param name="DisplayNames">Dictionary mapping permission strings to display names</param>
	public record SubmoduleInfo(string[] Permissions, Dictionary<string, string> DisplayNames);

	/// <summary>
	/// Helper record to store module metadata
	/// </summary>
	/// <param name="DisplayNames">Dictionary mapping module strings to display names</param>
	/// <param name="Submodules">Dictionary of submodules within the module</param>
	public record ModuleInfo(
		Dictionary<string, string> DisplayNames,
		Dictionary<string, SubmoduleInfo> Submodules);
}
