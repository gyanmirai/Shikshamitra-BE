using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

/// <summary>
/// Students module - Notification related permissions
/// </summary>
public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class Students
	{
		/// <summary>
		/// Student Notification - Inquiry Management permissions
		/// </summary>
		public static class Notification_InquiryManagement
		{
			private const string SubmoduleName = $"{ModuleName}.Notification.InquiryManagement";
			private const string SubmoduleDisplayName = $"{DisplayName}.Notification.InquiryManagement";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";

			public static readonly string[] All = { View, Add, Delete };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		/// <summary>
		/// Student Notification - Registration Management permissions
		/// </summary>
		public static class Notification_RegistrationManagement
		{
			private const string SubmoduleName = $"{ModuleName}.Notification.RegistrationManagement";
			private const string SubmoduleDisplayName = $"{DisplayName}.Notification.RegistrationManagement";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";

			public static readonly string[] All = { View, Add, Delete };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		/// <summary>
		/// Student Notification - Admission Management permissions
		/// </summary>
		public static class Notification_AdmissionManagement
		{
			private const string SubmoduleName = $"{ModuleName}.Notification.AdmissionManagement";
			private const string SubmoduleDisplayName = $"{DisplayName}.Notification.AdmissionManagement";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";

			public static readonly string[] All = { View, Add, Delete };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		/// <summary>
		/// Student Notification - Student Record Management permissions
		/// </summary>
		public static class Notification_StudentRecordManagement
		{
			private const string SubmoduleName = $"{ModuleName}.Notification.StudentRecordManagement";
			private const string SubmoduleDisplayName = $"{DisplayName}.Notification.StudentRecordManagement";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";

			public static readonly string[] All = { View, Add, Delete };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		/// <summary>
		/// Student Notification - Attendance Management permissions
		/// </summary>
		public static class Notification_AttendanceManagement
		{
			private const string SubmoduleName = $"{ModuleName}.Notification.AttendanceManagement";
			private const string SubmoduleDisplayName = $"{DisplayName}.Notification.AttendanceManagement";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";

			public static readonly string[] All = { View, Add, Delete };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}
	}
}
