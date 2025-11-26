using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

/// <summary>
/// Students module permissions and submodules
/// </summary>
public static partial class ShikshaMitraSubmodulePermissions
{
	/// <summary>
	/// Students module permissions
	/// </summary>
	public static class Students
	{
		/// <summary>
		/// Permission group name for Students module.
		/// This creates a separate permission group in ABP for better organization.
		/// </summary>
		public const string GroupName = "Students";

		/// <summary>
		/// Display name for Students module permission group
		/// </summary>
		public const string GroupDisplayName = "Permission:StudentsModule";

		private const string ModuleName = $"{ShikshaMitraSubmodulePermissions.GroupName}.Students";
		private const string DisplayName = $"{DisplayNamePrefix}:Students";

		public const string Default = ModuleName;

		// Student Records Submodule
		public static class StudentRecords
		{
			private const string SubmoduleName = $"{ModuleName}.StudentRecords";
			private const string SubmoduleDisplayName = $"{DisplayName}.StudentRecords";

			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public const string Create = $"{SubmoduleName}.Create";
			public const string Edit = $"{SubmoduleName}.Edit";
			public const string Delete = $"{SubmoduleName}.Delete";
			public const string BulkUpload = $"{SubmoduleName}.BulkUpload";
			public const string Export = $"{SubmoduleName}.Export";

			public static readonly string[] All = { Default, View, Create, Edit, Delete, BulkUpload, Export };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Create, $"{SubmoduleDisplayName}.Create" },
				{ Edit, $"{SubmoduleDisplayName}.Edit" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" },
				{ BulkUpload, $"{SubmoduleDisplayName}.BulkUpload" },
				{ Export, $"{SubmoduleDisplayName}.Export" }
			};
		}

		// Admission Submodule
		public static class Admission
		{
			private const string SubmoduleName = $"{ModuleName}.Admission";
			private const string SubmoduleDisplayName = $"{DisplayName}.Admission";

			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public const string Create = $"{SubmoduleName}.Create";
			public const string Edit = $"{SubmoduleName}.Edit";
			public const string Delete = $"{SubmoduleName}.Delete";
			public const string Approve = $"{SubmoduleName}.Approve";
			public const string Reject = $"{SubmoduleName}.Reject";

			public static readonly string[] All = { Default, View, Create, Edit, Delete, Approve, Reject };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Create, $"{SubmoduleDisplayName}.Create" },
				{ Edit, $"{SubmoduleDisplayName}.Edit" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" },
				{ Approve, $"{SubmoduleDisplayName}.Approve" },
				{ Reject, $"{SubmoduleDisplayName}.Reject" }
			};
		}

		// Attendance Submodule
		public static class Attendance
		{
			private const string SubmoduleName = $"{ModuleName}.Attendance";
			private const string SubmoduleDisplayName = $"{DisplayName}.Attendance";

			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public const string Mark = $"{SubmoduleName}.Mark";
			public const string Edit = $"{SubmoduleName}.Edit";
			public const string Reports = $"{SubmoduleName}.Reports";

			public static readonly string[] All = { Default, View, Mark, Edit, Reports };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Mark, $"{SubmoduleDisplayName}.Mark" },
				{ Edit, $"{SubmoduleDisplayName}.Edit" },
				{ Reports, $"{SubmoduleDisplayName}.Reports" }
			};
		}

		// Promotion Submodule
		public static class Promotion
		{
			private const string SubmoduleName = $"{ModuleName}.Promotion";
			private const string SubmoduleDisplayName = $"{DisplayName}.Promotion";

			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public const string Process = $"{SubmoduleName}.Process";
			public const string Approve = $"{SubmoduleName}.Approve";

			public static readonly string[] All = { Default, View, Process, Approve };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Process, $"{SubmoduleDisplayName}.Process" },
				{ Approve, $"{SubmoduleDisplayName}.Approve" }
			};
		}

		public static readonly Dictionary<string, string> DisplayNames = new()
		{
			{ Default, DisplayName }
		};

		// Helper to get all submodules
		public static readonly Dictionary<string, SubmoduleInfo> Submodules = new()
		{
			{ StudentRecords.Default, new SubmoduleInfo(StudentRecords.All, StudentRecords.DisplayNames) },
			{ Admission.Default, new SubmoduleInfo(Admission.All, Admission.DisplayNames) },
			{ Attendance.Default, new SubmoduleInfo(Attendance.All, Attendance.DisplayNames) },
			{ Promotion.Default, new SubmoduleInfo(Promotion.All, Promotion.DisplayNames) }
		};
	}
}
