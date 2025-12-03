using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

/// <summary>
/// Students module - Reports related permissions
/// </summary>
public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class Students
	{
		/// <summary>
		/// Student Reports permissions
		/// </summary>
		public static class Reports
		{
			private const string SubmoduleName = $"{ModuleName}.Reports";
			private const string SubmoduleDisplayName = $"{DisplayName}.Reports";

			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";

			public static readonly string[] All = { Default, View, Add };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" }
			};
		}
	}
}
