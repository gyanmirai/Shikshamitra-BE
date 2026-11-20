using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class Library
	{
		public static class Settings_CatalogueConfiguration
		{
			public const string View = "Library.Settings.CatalogueConfiguration.View";
			public const string Update = "Library.Settings.CatalogueConfiguration.Update";
			public static readonly string[] All = [View, Update];
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ "Library.Settings.CatalogueConfiguration", "Permission:Library.Settings.CatalogueConfiguration" },
				{ View, "Permission:Library.Settings.CatalogueConfiguration.View" },
				{ Update, "Permission:Library.Settings.CatalogueConfiguration.Update" }
			};
		}
	}
}
