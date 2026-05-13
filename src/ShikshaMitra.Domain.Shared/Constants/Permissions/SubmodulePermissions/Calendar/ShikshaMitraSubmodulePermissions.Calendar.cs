using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

/// <summary>
/// Calendar module permissions and submodules.
/// This file is split into multiple partial files for better maintainability:
/// - ShikshaMitraSubmodulePermissions.Calendar.cs (Main module definition)
/// - ShikshaMitraSubmodulePermissions.Calendar.Settings.cs
/// </summary>
public static partial class ShikshaMitraSubmodulePermissions
{
	/// <summary>
	/// Calendar module permissions
	/// </summary>
	public static partial class Calendar
	{
		/// <summary>
		/// Permission group name for Calendar module.
		/// </summary>
		public const string GroupName = "Calendar";

		/// <summary>
		/// Display name for Calendar module permission group.
		/// </summary>
		public const string GroupDisplayName = "Permission:CalendarModule";

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
			{ "Calendar.Settings.EventType", new SubmoduleInfo(Settings_EventType.All, Settings_EventType.DisplayNames) },
			{ "Calendar.Settings.HolidayType", new SubmoduleInfo(Settings_HolidayType.All, Settings_HolidayType.DisplayNames) }
		};
	}
}
