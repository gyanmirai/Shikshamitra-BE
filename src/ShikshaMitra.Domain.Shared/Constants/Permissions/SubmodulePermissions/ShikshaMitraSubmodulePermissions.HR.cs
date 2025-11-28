using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

/// <summary>
/// HR module permissions and submodules
/// </summary>
public static partial class ShikshaMitraSubmodulePermissions
{
	/// <summary>
	/// HR module permissions
	/// </summary>
	public static class HR
	{
		/// <summary>
		/// Permission group name for HR module.
		/// This creates a separate permission group in ABP for better organization.
		/// </summary>
		public const string GroupName = "HR";
		
		/// <summary>
		/// Display name for HR module permission group
		/// </summary>
		public const string GroupDisplayName = "Permission:HRModule";

		private const string ModuleName = $"{ShikshaMitraSubmodulePermissions.GroupName}.HR";
		private const string DisplayName = $"{DisplayNamePrefix}:HR";

		public const string Default = ModuleName;

		// Employee Management Submodule
		public static class EmployeeManagement
		{
			private const string SubmoduleName = $"{ModuleName}.EmployeeManagement";
			private const string SubmoduleDisplayName = $"{DisplayName}.EmployeeManagement";

			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public const string Create = $"{SubmoduleName}.Create";
			public const string Edit = $"{SubmoduleName}.Edit";
			public const string Delete = $"{SubmoduleName}.Delete";
			public const string ViewSalary = $"{SubmoduleName}.ViewSalary";

			public static readonly string[] All = { Default, View, Create, Edit, Delete, ViewSalary };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Create, $"{SubmoduleDisplayName}.Create" },
				{ Edit, $"{SubmoduleDisplayName}.Edit" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" },
				{ ViewSalary, $"{SubmoduleDisplayName}.ViewSalary" }
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

		// Payroll Submodule
		public static class Payroll
		{
			private const string SubmoduleName = $"{ModuleName}.Payroll";
			private const string SubmoduleDisplayName = $"{DisplayName}.Payroll";

			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public const string Process = $"{SubmoduleName}.Process";
			public const string Edit = $"{SubmoduleName}.Edit";
			public const string Approve = $"{SubmoduleName}.Approve";
			public const string Generate = $"{SubmoduleName}.Generate";

			public static readonly string[] All = { Default, View, Process, Edit, Approve, Generate };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Process, $"{SubmoduleDisplayName}.Process" },
				{ Edit, $"{SubmoduleDisplayName}.Edit" },
				{ Approve, $"{SubmoduleDisplayName}.Approve" },
				{ Generate, $"{SubmoduleDisplayName}.Generate" }
			};
		}

		// Leave Management Submodule
		public static class LeaveManagement
		{
			private const string SubmoduleName = $"{ModuleName}.LeaveManagement";
			private const string SubmoduleDisplayName = $"{DisplayName}.LeaveManagement";

			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public const string Apply = $"{SubmoduleName}.Apply";
			public const string Approve = $"{SubmoduleName}.Approve";
			public const string Reject = $"{SubmoduleName}.Reject";
			public const string Cancel = $"{SubmoduleName}.Cancel";

			public static readonly string[] All = { Default, View, Apply, Approve, Reject, Cancel };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Apply, $"{SubmoduleDisplayName}.Apply" },
				{ Approve, $"{SubmoduleDisplayName}.Approve" },
				{ Reject, $"{SubmoduleDisplayName}.Reject" },
				{ Cancel, $"{SubmoduleDisplayName}.Cancel" }
			};
		}

		public static readonly Dictionary<string, string> DisplayNames = new()
		{
			{ Default, DisplayName }
		};

		public static readonly Dictionary<string, SubmoduleInfo> Submodules = new()
		{
			{ EmployeeManagement.Default, new SubmoduleInfo(EmployeeManagement.All, EmployeeManagement.DisplayNames) },
			{ Attendance.Default, new SubmoduleInfo(Attendance.All, Attendance.DisplayNames) },
			{ Payroll.Default, new SubmoduleInfo(Payroll.All, Payroll.DisplayNames) },
			{ LeaveManagement.Default, new SubmoduleInfo(LeaveManagement.All, LeaveManagement.DisplayNames) }
		};
	}
}
