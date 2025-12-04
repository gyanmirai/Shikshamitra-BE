using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

/// <summary>
/// Master registry of all modules
/// </summary>
public static partial class ShikshaMitraSubmodulePermissions
{
	/// <summary>
	/// Master registry of all modules in the system.
	/// This dictionary provides a centralized way to access all module and submodule information.
	/// </summary>
	public static readonly Dictionary<string, ModuleInfo> AllModules = new()
	{
		{ Students.Default, new ModuleInfo(Students.DisplayNames, Students.Submodules) },
		{ Academics.Default, new ModuleInfo(Academics.DisplayNames, Academics.Submodules) },
		{ HR.Default, new ModuleInfo(HR.DisplayNames, HR.Submodules) },
		{ ClassManagement.Default, new ModuleInfo(ClassManagement.DisplayNames, ClassManagement.Submodules) }
	};
}
