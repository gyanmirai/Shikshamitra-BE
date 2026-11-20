using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class Library
	{
		public static class Receiving
		{
			public const string View = "Library.Receiving.View";
			public const string Add = "Library.Receiving.Add";
			public static readonly string[] All = [View, Add];
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, "Permission:Library.Receiving.View" },
				{ Add, "Permission:Library.Receiving.Add" }
			};
		}
	}
}
