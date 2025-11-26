using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

/// <summary>
/// Class Management module permissions and submodules
/// </summary>
public static partial class ShikshaMitraSubmodulePermissions
{
	/// <summary>
	/// Class Management module permissions
	/// </summary>
	public static class ClassManagement
	{
		/// <summary>
		/// Permission group name for Class Management module.
		/// This creates a separate permission group in ABP for better organization.
		/// </summary>
		public const string GroupName = "ClassManagement";
		
		/// <summary>
		/// Display name for Class Management module permission group
		/// </summary>
		public const string GroupDisplayName = "Permission:ClassManagementModule";

		private const string ModuleName = $"{ShikshaMitraSubmodulePermissions.GroupName}.ClassManagement";
		private const string DisplayName = $"{DisplayNamePrefix}:ClassManagement";

		public const string Default = ModuleName;

		// Classes Submodule
		public static class Classes
		{
			private const string SubmoduleName = $"{ModuleName}.Classes";
			private const string SubmoduleDisplayName = $"{DisplayName}.Classes";

			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public const string Create = $"{SubmoduleName}.Create";
			public const string Edit = $"{SubmoduleName}.Edit";
			public const string Delete = $"{SubmoduleName}.Delete";

			public static readonly string[] All = { Default, View, Create, Edit, Delete };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Create, $"{SubmoduleDisplayName}.Create" },
				{ Edit, $"{SubmoduleDisplayName}.Edit" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		// Sections Submodule
		public static class Sections
		{
			private const string SubmoduleName = $"{ModuleName}.Sections";
			private const string SubmoduleDisplayName = $"{DisplayName}.Sections";

			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public const string Create = $"{SubmoduleName}.Create";
			public const string Edit = $"{SubmoduleName}.Edit";
			public const string Delete = $"{SubmoduleName}.Delete";
			public const string AssignStudents = $"{SubmoduleName}.AssignStudents";

			public static readonly string[] All = { Default, View, Create, Edit, Delete, AssignStudents };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Create, $"{SubmoduleDisplayName}.Create" },
				{ Edit, $"{SubmoduleDisplayName}.Edit" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" },
				{ AssignStudents, $"{SubmoduleDisplayName}.AssignStudents" }
			};
		}

		public static readonly Dictionary<string, string> DisplayNames = new()
		{
			{ Default, DisplayName }
		};

		public static readonly Dictionary<string, SubmoduleInfo> Submodules = new()
		{
			{ Classes.Default, new SubmoduleInfo(Classes.All, Classes.DisplayNames) },
			{ Sections.Default, new SubmoduleInfo(Sections.All, Sections.DisplayNames) }
		};
	}
}
