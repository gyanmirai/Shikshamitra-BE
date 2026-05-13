using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

/// <summary>
/// Library module permissions and submodules.
/// This file is split into multiple partial files for better maintainability:
/// - ShikshaMitraSubmodulePermissions.Library.cs (Main module definition)
/// - ShikshaMitraSubmodulePermissions.Library.Settings.cs
/// </summary>
public static partial class ShikshaMitraSubmodulePermissions
{
	/// <summary>
	/// Library module permissions
	/// </summary>
	public static partial class Library
	{
		/// <summary>
		/// Permission group name for Library module.
		/// </summary>
		public const string GroupName = "Library";

		/// <summary>
		/// Display name for Library module permission group.
		/// </summary>
		public const string GroupDisplayName = "Permission:LibraryModule";

		private const string ModuleName = GroupName;
		private const string DisplayName = $"{DisplayNamePrefix}:{GroupName}";

		public const string Default = ModuleName;

		public static readonly Dictionary<string, string> DisplayNames = new()
		{
			{ Default, DisplayName }
		};

		public static readonly Dictionary<string, SubmoduleInfo> Submodules = new()
		{
			// Settings submodules
			{ "Library.Settings.BookCategories", new SubmoduleInfo(Settings_BookCategories.All, Settings_BookCategories.DisplayNames) },
			{ "Library.Settings.ChargesCategories", new SubmoduleInfo(Settings_ChargesCategories.All, Settings_ChargesCategories.DisplayNames) },
			{ "Library.Settings.LibraryHours", new SubmoduleInfo(Settings_LibraryHours.All, Settings_LibraryHours.DisplayNames) },
			{ "Library.Settings.BorrowingRules", new SubmoduleInfo(Settings_BorrowingRules.All, Settings_BorrowingRules.DisplayNames) },
			{ "Library.Settings.SectionManagement", new SubmoduleInfo(Settings_SectionManagement.All, Settings_SectionManagement.DisplayNames) }
		};
	}
}
