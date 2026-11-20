using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class Library
	{
		public static class CampusTransfers
		{
			public const string View = "Library.CampusTransfers.View";
			public const string Dispatch = "Library.CampusTransfers.Dispatch";
			public const string Receive = "Library.CampusTransfers.Receive";
			public static readonly string[] All = [View, Dispatch, Receive];
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, "Permission:Library.CampusTransfers.View" },
				{ Dispatch, "Permission:Library.CampusTransfers.Dispatch" },
				{ Receive, "Permission:Library.CampusTransfers.Receive" }
			};
		}
	}
}
