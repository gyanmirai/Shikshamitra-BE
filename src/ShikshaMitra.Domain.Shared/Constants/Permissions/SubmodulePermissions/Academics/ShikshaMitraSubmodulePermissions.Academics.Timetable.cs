using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

/// <summary>
/// Academics module - Timetable related permissions
/// </summary>
public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class Academics
	{
		/// <summary>
		/// Timetable - Timetable Management permissions
		/// </summary>
		public static class Timetable_TimetableManagement
		{
			private const string SubmoduleName = $"{ModuleName}.Timetable.TimetableManagement";
			private const string SubmoduleDisplayName = $"{DisplayName}.Timetable.TimetableManagement";

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

		/// <summary>
		/// Timetable - Reports permissions
		/// </summary>
		public static class Timetable_Reports
		{
			private const string SubmoduleName = $"{ModuleName}.Timetable.Reports";
			private const string SubmoduleDisplayName = $"{DisplayName}.Timetable.Reports";

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

		/// <summary>
		/// Timetable - Settings - Weekly Lectures permissions
		/// </summary>
		public static class Timetable_SettingsWeeklyLectures
		{
			private const string SubmoduleName = $"{ModuleName}.Timetable.Settings.WeeklyLectures";
			private const string SubmoduleDisplayName = $"{DisplayName}.Timetable.Settings.WeeklyLectures";

			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Update = $"{SubmoduleName}.Update";

			public static readonly string[] All = { Default, View, Add, Update };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Update, $"{SubmoduleDisplayName}.Update" }
			};
		}

		/// <summary>
		/// Timetable - Settings - Lecture Slots permissions
		/// </summary>
		public static class Timetable_SettingsLectureSlots
		{
			private const string SubmoduleName = $"{ModuleName}.Timetable.Settings.LectureSlots";
			private const string SubmoduleDisplayName = $"{DisplayName}.Timetable.Settings.LectureSlots";

			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Update = $"{SubmoduleName}.Update";

			public static readonly string[] All = { Default, View, Add, Update };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Update, $"{SubmoduleDisplayName}.Update" }
			};
		}
	}
}
