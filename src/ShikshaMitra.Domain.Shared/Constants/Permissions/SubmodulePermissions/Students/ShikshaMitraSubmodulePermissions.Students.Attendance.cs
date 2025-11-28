using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

/// <summary>
/// Students module - Attendance related permissions
/// </summary>
public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class Students
	{
		/// <summary>
		/// Student Attendance - Mark Attendance permissions
		/// </summary>
		public static class Attendance_MarkAttendance
		{
			private const string SubmoduleName = $"{ModuleName}.Attendance.MarkAttendance";
			private const string SubmoduleDisplayName = $"{DisplayName}.Attendance.MarkAttendance";

			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";

			public static readonly string[] All = { Default, View };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" }
			};
		}

		/// <summary>
		/// Student Attendance - Leave and Absent permissions
		/// </summary>
		public static class Attendance_LeaveAndAbsent
		{
			private const string SubmoduleName = $"{ModuleName}.Attendance.LeaveAndAbsent";
			private const string SubmoduleDisplayName = $"{DisplayName}.Attendance.LeaveAndAbsent";

			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Update = $"{SubmoduleName}.Update";
			public const string Delete = $"{SubmoduleName}.Delete";

			public static readonly string[] All = { Default, View, Add, Update, Delete };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Update, $"{SubmoduleDisplayName}.Update" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		/// <summary>
		/// Student Attendance - Reports permissions
		/// </summary>
		public static class Attendance_Reports
		{
			private const string SubmoduleName = $"{ModuleName}.Attendance.Reports";
			private const string SubmoduleDisplayName = $"{DisplayName}.Attendance.Reports";

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
		/// Student Attendance - Setting - Leave Type permissions
		/// </summary>
		public static class Attendance_SettingLeaveType
		{
			private const string SubmoduleName = $"{ModuleName}.Attendance.Setting.LeaveType";
			private const string SubmoduleDisplayName = $"{DisplayName}.Attendance.Setting.LeaveType";

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
