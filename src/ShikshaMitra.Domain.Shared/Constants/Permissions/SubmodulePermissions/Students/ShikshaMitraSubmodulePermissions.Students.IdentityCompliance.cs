using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class Students
	{
		public static class IdentityCompliance
		{
			private const string SubmoduleName = $"{ModuleName}.IdentityCompliance";
			private const string SubmoduleDisplayName = $"{DisplayName}.IdentityCompliance";

			public const string View = $"{SubmoduleName}.View";
			public const string ManageSensitive = $"{SubmoduleName}.ManageSensitive";

			public static readonly string[] All = [View, ManageSensitive];

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ ManageSensitive, $"{SubmoduleDisplayName}.ManageSensitive" }
			};
		}
	}
}
