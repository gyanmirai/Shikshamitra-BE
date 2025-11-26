using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

/// <summary>
/// Dashboard module permissions and submodules
/// </summary>
public static partial class ShikshaMitraSubmodulePermissions
{
	/// <summary>
	/// Dashboard module permissions
	/// </summary>
	public static class Dashboard
	{
		/// <summary>
		/// Permission group name for Dashboard module.
		/// This creates a separate permission group in ABP for better organization.
		/// </summary>
		public const string GroupName = "Dashboard";
		
		/// <summary>
		/// Display name for Dashboard module permission group
		/// </summary>
		public const string GroupDisplayName = "Permission:DashboardModule";

		private const string ModuleName = $"{ShikshaMitraSubmodulePermissions.GroupName}.Dashboard";
		private const string DisplayName = $"{DisplayNamePrefix}:Dashboard";

		public const string Default = ModuleName;

		// Analytics Submodule
		public static class Analytics
		{
			private const string SubmoduleName = $"{ModuleName}.Analytics";
			private const string SubmoduleDisplayName = $"{DisplayName}.Analytics";

			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public const string Advanced = $"{SubmoduleName}.Advanced";

			public static readonly string[] All = { Default, View, Advanced };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Advanced, $"{SubmoduleDisplayName}.Advanced" }
			};
		}

		// Reports Submodule
		public static class Reports
		{
			private const string SubmoduleName = $"{ModuleName}.Reports";
			private const string SubmoduleDisplayName = $"{DisplayName}.Reports";

			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public const string Export = $"{SubmoduleName}.Export";
			public const string Schedule = $"{SubmoduleName}.Schedule";

			public static readonly string[] All = { Default, View, Export, Schedule };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Export, $"{SubmoduleDisplayName}.Export" },
				{ Schedule, $"{SubmoduleDisplayName}.Schedule" }
			};
		}

		public static readonly Dictionary<string, string> DisplayNames = new()
		{
			{ Default, DisplayName }
		};

		public static readonly Dictionary<string, SubmoduleInfo> Submodules = new()
		{
			{ Analytics.Default, new SubmoduleInfo(Analytics.All, Analytics.DisplayNames) },
			{ Reports.Default, new SubmoduleInfo(Reports.All, Reports.DisplayNames) }
		};
	}
}
