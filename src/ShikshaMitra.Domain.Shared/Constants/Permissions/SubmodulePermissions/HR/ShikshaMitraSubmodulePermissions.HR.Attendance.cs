using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class HR
	{
		public static class AttendanceAndLeave_MarkAttendance
		{
			private const string SubmoduleName = $"{ModuleName}.AttendanceAndLeave.MarkAttendance";
			private const string SubmoduleDisplayName = $"{DisplayName}.AttendanceAndLeave.MarkAttendance";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";

			public static readonly string[] All = { View, Add, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static class AttendanceAndLeave_BiometricAttendance
		{
			private const string SubmoduleName = $"{ModuleName}.AttendanceAndLeave.BiometricAttendance";
			private const string SubmoduleDisplayName = $"{DisplayName}.AttendanceAndLeave.BiometricAttendance";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";

			public static readonly string[] All = { View, Add, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static class AttendanceAndLeave_SettingShiftManagement
		{
			private const string SubmoduleName = $"{ModuleName}.AttendanceAndLeave.Setting.ShiftManagement";
			private const string SubmoduleDisplayName = $"{DisplayName}.AttendanceAndLeave.Setting.ShiftManagement";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";

			public static readonly string[] All = { View, Add, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static class AttendanceAndLeave_SettingAttendanceConfiguration
		{
			private const string SubmoduleName = $"{ModuleName}.AttendanceAndLeave.Setting.AttendanceConfiguration";
			private const string SubmoduleDisplayName = $"{DisplayName}.AttendanceAndLeave.Setting.AttendanceConfiguration";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";

			public static readonly string[] All = { View, Add, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}
	}
}
