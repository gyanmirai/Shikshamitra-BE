using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class Library
	{
		public static class Custody
		{
			public const string View = "Library.Custody.View";
			public const string Add = "Library.Custody.Add";
			public const string Return = "Library.Custody.Return";
			public static readonly string[] All = [View, Add, Return];
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, "Permission:Library.Custody.View" },
				{ Add, "Permission:Library.Custody.Add" },
				{ Return, "Permission:Library.Custody.Return" }
			};
		}
	}
}
