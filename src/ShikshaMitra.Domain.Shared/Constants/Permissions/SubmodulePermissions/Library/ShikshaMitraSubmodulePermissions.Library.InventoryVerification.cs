using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class Library
	{
		public static class InventoryVerification
		{
			public const string View = "Library.InventoryVerification.View";
			public const string Add = "Library.InventoryVerification.Add";
			public const string Update = "Library.InventoryVerification.Update";
			public const string Approve = "Library.InventoryVerification.Approve";
			public static readonly string[] All = [View, Add, Update, Approve];
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, "Permission:Library.InventoryVerification.View" },
				{ Add, "Permission:Library.InventoryVerification.Add" },
				{ Update, "Permission:Library.InventoryVerification.Update" },
				{ Approve, "Permission:Library.InventoryVerification.Approve" }
			};
		}
	}
}
