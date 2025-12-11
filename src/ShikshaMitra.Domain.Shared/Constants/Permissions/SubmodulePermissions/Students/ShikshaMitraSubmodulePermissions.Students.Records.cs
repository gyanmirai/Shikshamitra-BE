using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

/// <summary>
/// Students module - Records related permissions
/// </summary>
public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class Students
	{
		/// <summary>
		/// Student Records - Student List permissions
		/// </summary>
		public static class Records_StudentList
		{
			private const string SubmoduleName = $"{ModuleName}.Records.StudentList";
			private const string SubmoduleDisplayName = $"{DisplayName}.Records.StudentList";

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
		/// Student Records - Student I-Card permissions
		/// </summary>
		public static class Records_StudentICard
		{
			private const string SubmoduleName = $"{ModuleName}.Records.StudentICard";
			private const string SubmoduleDisplayName = $"{DisplayName}.Records.StudentICard";

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
		/// Student Records - Student Management permissions
		/// </summary>
		public static class Records_StudentManagement
		{
			private const string SubmoduleName = $"{ModuleName}.Records.StudentManagement";
			private const string SubmoduleDisplayName = $"{DisplayName}.Records.StudentManagement";

			public const string StudentManagementView = $"{SubmoduleName}.StudentManagement.View";
			public const string StudentManagementAdd = $"{SubmoduleName}.StudentManagement.Add";
			public const string StudentManagementDelete = $"{SubmoduleName}.StudentManagement.Delete";

			public static readonly string[] All = { 
				
				StudentManagementView, StudentManagementAdd, StudentManagementDelete
			};

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ StudentManagementView, $"{SubmoduleDisplayName}.StudentManagement.View" },
				{ StudentManagementAdd, $"{SubmoduleDisplayName}.StudentManagement.Add" },
				{ StudentManagementDelete, $"{SubmoduleDisplayName}.StudentManagement.Delete" },
			};
		}

		/// <summary>
		/// Student Records - Certificates
		/// </summary>
		public static class Records_Certificates
		{
			private const string SubmoduleName = $"{ModuleName}.Records.Certificates";
			private const string SubmoduleDisplayName = $"{DisplayName}.Records.Certificates";

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
		/// Student Records - Settings permissions
		/// </summary>
		public static class Records_Settings
		{
			private const string SubmoduleName = $"{ModuleName}.Records.Settings";
			private const string SubmoduleDisplayName = $"{DisplayName}.Records.Settings";

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
