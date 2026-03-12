using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

/// <summary>
/// InstituteSetup module permissions and submodules
/// </summary>
public static partial class ShikshaMitraSubmodulePermissions
{
	/// <summary>
	/// InstituteSetup module permissions
	/// </summary>
	public static partial class InstituteSetup
	{
		/// <summary>
		/// Permission group name for InstituteSetup module
		/// </summary>
		public const string GroupName = "InstituteSetup";

		/// <summary>
		/// Display name for InstituteSetup module permission group
		/// </summary>
		public const string GroupDisplayName = "Permission:InstituteSetupModule";

		private const string ModuleName = GroupName;
		private const string DisplayName = $"{DisplayNamePrefix}:{GroupName}";

		public const string Default = ModuleName;

		public static readonly Dictionary<string, string> DisplayNames = new()
		{
			{ Default, DisplayName }
		};

		// Basic Information
		public static class BasicInformation
		{
			private const string SubmoduleName = $"{ModuleName}.BasicInformation";
			private const string SubmoduleDisplayName = $"{DisplayName}.BasicInformation";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Edit = $"{SubmoduleName}.Edit";
			public const string Delete = $"{SubmoduleName}.Delete";

			public static readonly string[] All = { View, Add, Edit, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Edit, $"{SubmoduleDisplayName}.Edit" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		// Academic Setup
		public static class AcademicSetup
		{
			private const string SubmoduleName = $"{ModuleName}.AcademicSetup";
			private const string SubmoduleDisplayName = $"{DisplayName}.AcademicSetup";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Edit = $"{SubmoduleName}.Edit";
			public const string Delete = $"{SubmoduleName}.Delete";

			public static readonly string[] All = { View, Add, Edit, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Edit, $"{SubmoduleDisplayName}.Edit" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		// Shift Management
		public static class ShiftManagement
		{
			private const string SubmoduleName = $"{ModuleName}.ShiftManagement";
			private const string SubmoduleDisplayName = $"{DisplayName}.ShiftManagement";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Edit = $"{SubmoduleName}.Edit";
			public const string Delete = $"{SubmoduleName}.Delete";

			public static readonly string[] All = { View, Add, Edit, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Edit, $"{SubmoduleDisplayName}.Edit" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		// Rooms Management
		public static class RoomsManagement
		{
			private const string SubmoduleName = $"{ModuleName}.RoomsManagement";
			private const string SubmoduleDisplayName = $"{DisplayName}.RoomsManagement";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Edit = $"{SubmoduleName}.Edit";
			public const string Delete = $"{SubmoduleName}.Delete";

			public static readonly string[] All = { View, Add, Edit, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Edit, $"{SubmoduleDisplayName}.Edit" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static readonly Dictionary<string, SubmoduleInfo> Submodules = new()
		{
			{ "InstituteSetup.BasicInformation", new SubmoduleInfo(BasicInformation.All, BasicInformation.DisplayNames) },
			{ "InstituteSetup.AcademicSetup", new SubmoduleInfo(AcademicSetup.All, AcademicSetup.DisplayNames) },
			{ "InstituteSetup.ShiftManagement", new SubmoduleInfo(ShiftManagement.All, ShiftManagement.DisplayNames) },
			{ "InstituteSetup.RoomsManagement", new SubmoduleInfo(RoomsManagement.All, RoomsManagement.DisplayNames) }
		};
	}
}
