using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class HR
	{
		public static class AttendanceAndLeave_Dashboard
		{
			private const string SubmoduleName = $"{ModuleName}.AttendanceAndLeave.Dashboard";
			private const string SubmoduleDisplayName = $"{DisplayName}.AttendanceAndLeave.Dashboard";

			public const string View = $"{SubmoduleName}.View";

			public static readonly string[] All = { View };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" }
			};
		}

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

		public static class AttendanceAndLeave_AttendanceTracking
		{
			private const string SubmoduleName = $"{ModuleName}.AttendanceAndLeave.AttendanceTracking";
			private const string SubmoduleDisplayName = $"{DisplayName}.AttendanceAndLeave.AttendanceTracking";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";

			public static readonly string[] All = { View, Add };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" }
			};
		}

		public static class AttendanceAndLeave_Evaluations
		{
			private const string SubmoduleName = $"{ModuleName}.AttendanceAndLeave.Evaluations";
			private const string SubmoduleDisplayName = $"{DisplayName}.AttendanceAndLeave.Evaluations";

			public const string ViewAudit = $"{SubmoduleName}.ViewAudit";

			public static readonly string[] All = { ViewAudit };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ ViewAudit, $"{SubmoduleDisplayName}.ViewAudit" }
			};
		}

	}
}
