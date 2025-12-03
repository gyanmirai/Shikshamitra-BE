using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class Academics
	{
		public static class GroupManagement_Dashboard
		{
			private const string SubmoduleName = $"{ModuleName}.GroupManagement.Dashboard";
			private const string SubmoduleDisplayName = $"{DisplayName}.GroupManagement.Dashboard";
			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public static readonly string[] All = { Default, View };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName }, { View, $"{SubmoduleDisplayName}.View" }
			};
		}

		public static class GroupManagement_DynamicGroup
		{
			private const string SubmoduleName = $"{ModuleName}.GroupManagement.DynamicGroup";
			private const string SubmoduleDisplayName = $"{DisplayName}.GroupManagement.DynamicGroup";
			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Update = $"{SubmoduleName}.Update";
			public const string Delete = $"{SubmoduleName}.Delete";
			public static readonly string[] All = { Default, View, Add, Update, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName }, { View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" }, { Update, $"{SubmoduleDisplayName}.Update" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static class GroupManagement_ActivityManagement
		{
			private const string SubmoduleName = $"{ModuleName}.GroupManagement.ActivityManagement";
			private const string SubmoduleDisplayName = $"{DisplayName}.GroupManagement.ActivityManagement";
			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Update = $"{SubmoduleName}.Update";
			public const string Delete = $"{SubmoduleName}.Delete";
			public static readonly string[] All = { Default, View, Add, Update, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName }, { View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" }, { Update, $"{SubmoduleDisplayName}.Update" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}
	}
}
