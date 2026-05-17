using System.Collections.Generic;
using static ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions;

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
		{ ClassManagement.Default, new ModuleInfo(ClassManagement.DisplayNames, ClassManagement.Submodules) },
		{ Finance.Default, new ModuleInfo(Finance.DisplayNames, Finance.Submodules) },
		{ Communication.Default, new ModuleInfo(Communication.DisplayNames, Communication.Submodules) },
		{ Calendar.Default, new ModuleInfo(Calendar.DisplayNames, Calendar.Submodules) },
		{ Library.Default, new ModuleInfo(Library.DisplayNames, Library.Submodules) },
		{ Transport.Default, new ModuleInfo(Transport.DisplayNames, Transport.Submodules) },
		{ InstituteSetup.Default, new ModuleInfo(InstituteSetup.DisplayNames, InstituteSetup.Submodules) }
	};
}
