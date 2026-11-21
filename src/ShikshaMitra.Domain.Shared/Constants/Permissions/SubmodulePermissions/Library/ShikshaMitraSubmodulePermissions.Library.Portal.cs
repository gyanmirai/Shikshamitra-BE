using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class Library
	{
		public static class Portal
		{
			public const string View = "Library.Portal.View";
			public const string Reserve = "Library.Portal.Reserve";
			public const string Renew = "Library.Portal.Renew";
			public static readonly string[] All = [View, Reserve, Renew];
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, "Permission:Library.Portal.View" },
				{ Reserve, "Permission:Library.Portal.Reserve" },
				{ Renew, "Permission:Library.Portal.Renew" }
			};
		}
	}
}
