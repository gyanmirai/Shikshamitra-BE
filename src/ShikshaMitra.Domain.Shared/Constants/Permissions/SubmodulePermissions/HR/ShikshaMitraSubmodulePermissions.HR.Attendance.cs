using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class HR
	{
		public static class AttendanceAndLeave_ImportAttendance
		{
			private const string SubmoduleName = $"{ModuleName}.AttendanceAndLeave.ImportAttendance";
			private const string SubmoduleDisplayName = $"{DisplayName}.AttendanceAndLeave.ImportAttendance";
			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public const string Update = $"{SubmoduleName}.Update";
			public static readonly string[] All = { Default, View, Update };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName }, { View, $"{SubmoduleDisplayName}.View" },
				{ Update, $"{SubmoduleDisplayName}.Update" }
			};
		}

		public static class AttendanceAndLeave_LeaveManagement
		{
			private const string SubmoduleName = $"{ModuleName}.AttendanceAndLeave.LeaveManagement";
			private const string SubmoduleDisplayName = $"{DisplayName}.AttendanceAndLeave.LeaveManagement";
			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public static readonly string[] All = { Default, View, Add };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName }, { View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" }
			};
		}

		public static class AttendanceAndLeave_AttendanceTracking
		{
			private const string SubmoduleName = $"{ModuleName}.AttendanceAndLeave.AttendanceTracking";
			private const string SubmoduleDisplayName = $"{DisplayName}.AttendanceAndLeave.AttendanceTracking";
			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public static readonly string[] All = { Default, View };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName }, { View, $"{SubmoduleDisplayName}.View" }
			};
		}

		public static class AttendanceAndLeave_SettingShiftManagement
		{
			private const string SubmoduleName = $"{ModuleName}.AttendanceAndLeave.Setting.ShiftManagement";
			private const string SubmoduleDisplayName = $"{DisplayName}.AttendanceAndLeave.Setting.ShiftManagement";
			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Update = $"{SubmoduleName}.Update";
			public const string Delete = $"{SubmoduleName}.Delete";
			public static readonly string[] All = { Default, View, Add, Update, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName }, { View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" }, { Update, $"{SubmoduleDisplayName}.Update" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static class AttendanceAndLeave_SettingLeaveType
		{
			private const string SubmoduleName = $"{ModuleName}.AttendanceAndLeave.Setting.LeaveType";
			private const string SubmoduleDisplayName = $"{DisplayName}.AttendanceAndLeave.Setting.LeaveType";
			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Update = $"{SubmoduleName}.Update";
			public const string Delete = $"{SubmoduleName}.Delete";
			public static readonly string[] All = { Default, View, Add, Update, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName }, { View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" }, { Update, $"{SubmoduleDisplayName}.Update" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static class AttendanceAndLeave_SettingLeavePolicy
		{
			private const string SubmoduleName = $"{ModuleName}.AttendanceAndLeave.Setting.LeavePolicy";
			private const string SubmoduleDisplayName = $"{DisplayName}.AttendanceAndLeave.Setting.LeavePolicy";
			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Update = $"{SubmoduleName}.Update";
			public const string Delete = $"{SubmoduleName}.Delete";
			public static readonly string[] All = { Default, View, Add, Update, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName }, { View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" }, { Update, $"{SubmoduleDisplayName}.Update" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}
	}
}
