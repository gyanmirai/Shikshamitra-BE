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

		public static class AttendanceAndLeave_LeaveManagement
		{
			private const string SubmoduleName = $"{ModuleName}.AttendanceAndLeave.LeaveManagement";
			private const string SubmoduleDisplayName = $"{DisplayName}.AttendanceAndLeave.LeaveManagement";

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

		public static class AttendanceAndLeave_AttendanceTracking
		{
			private const string SubmoduleName = $"{ModuleName}.AttendanceAndLeave.AttendanceTracking";
			private const string SubmoduleDisplayName = $"{DisplayName}.AttendanceAndLeave.AttendanceTracking";

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

		public static class AttendanceAndLeave_SettingLeaveType
		{
			private const string SubmoduleName = $"{ModuleName}.AttendanceAndLeave.Setting.LeaveType";
			private const string SubmoduleDisplayName = $"{DisplayName}.AttendanceAndLeave.Setting.LeaveType";

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

		public static class AttendanceAndLeave_SettingLeavePolicy
		{
			private const string SubmoduleName = $"{ModuleName}.AttendanceAndLeave.Setting.LeavePolicy";
			private const string SubmoduleDisplayName = $"{DisplayName}.AttendanceAndLeave.Setting.LeavePolicy";

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
