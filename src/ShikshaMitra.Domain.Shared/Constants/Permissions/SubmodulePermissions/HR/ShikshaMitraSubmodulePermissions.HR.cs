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
	public static partial class HR
	{
		/// <summary>
		/// Permission group name for HR module
		/// </summary>
		public const string GroupName = "HR";

		/// <summary>
		/// Display name for HR module permission group
		/// </summary>
		public const string GroupDisplayName = "Permission:HRModule";

		private const string ModuleName = GroupName;
		private const string DisplayName = $"{DisplayNamePrefix}:{GroupName}";

		public const string Default = ModuleName;

		public static readonly Dictionary<string, string> DisplayNames = new()
		{
			{ Default, DisplayName }
		};

		// Dashboard
		public static class Dashboard
		{
			private const string SubmoduleName = $"{ModuleName}.Dashboard";
			private const string SubmoduleDisplayName = $"{DisplayName}.Dashboard";
			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public static readonly string[] All = { Default, View };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName }, { View, $"{SubmoduleDisplayName}.View" }
			};
		}

		// Reports
		public static class Reports
		{
			private const string SubmoduleName = $"{ModuleName}.Reports";
			private const string SubmoduleDisplayName = $"{DisplayName}.Reports";
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

		// Notification
		public static class Notification
		{
			private const string SubmoduleName = $"{ModuleName}.Notification";
			private const string SubmoduleDisplayName = $"{DisplayName}.Notification";
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

		public static readonly Dictionary<string, SubmoduleInfo> Submodules = new()
		{
			{ Dashboard.Default, new SubmoduleInfo(Dashboard.All, Dashboard.DisplayNames) },
			{ Employee_EmployeeList.Default, new SubmoduleInfo(Employee_EmployeeList.All, Employee_EmployeeList.DisplayNames) },
			{ Employee_ICard.Default, new SubmoduleInfo(Employee_ICard.All, Employee_ICard.DisplayNames) },
			{ Employee_SettingDocumentTypes.Default, new SubmoduleInfo(Employee_SettingDocumentTypes.All, Employee_SettingDocumentTypes.DisplayNames) },
			{ PayrollManagement_Dashboard.Default, new SubmoduleInfo(PayrollManagement_Dashboard.All, PayrollManagement_Dashboard.DisplayNames) },
			{ PayrollManagement_RunPayroll.Default, new SubmoduleInfo(PayrollManagement_RunPayroll.All, PayrollManagement_RunPayroll.DisplayNames) },
			{ PayrollManagement_GenerateSalarySlip.Default, new SubmoduleInfo(PayrollManagement_GenerateSalarySlip.All, PayrollManagement_GenerateSalarySlip.DisplayNames) },
			{ PayrollManagement_SettingsEarningComponents.Default, new SubmoduleInfo(PayrollManagement_SettingsEarningComponents.All, PayrollManagement_SettingsEarningComponents.DisplayNames) },
			{ PayrollManagement_DeductionComponents.Default, new SubmoduleInfo(PayrollManagement_DeductionComponents.All, PayrollManagement_DeductionComponents.DisplayNames) },
			{ AttendanceAndLeave_ImportAttendance.Default, new SubmoduleInfo(AttendanceAndLeave_ImportAttendance.All, AttendanceAndLeave_ImportAttendance.DisplayNames) },
			{ AttendanceAndLeave_LeaveManagement.Default, new SubmoduleInfo(AttendanceAndLeave_LeaveManagement.All, AttendanceAndLeave_LeaveManagement.DisplayNames) },
			{ AttendanceAndLeave_AttendanceTracking.Default, new SubmoduleInfo(AttendanceAndLeave_AttendanceTracking.All, AttendanceAndLeave_AttendanceTracking.DisplayNames) },
			{ AttendanceAndLeave_SettingShiftManagement.Default, new SubmoduleInfo(AttendanceAndLeave_SettingShiftManagement.All, AttendanceAndLeave_SettingShiftManagement.DisplayNames) },
			{ AttendanceAndLeave_SettingLeaveType.Default, new SubmoduleInfo(AttendanceAndLeave_SettingLeaveType.All, AttendanceAndLeave_SettingLeaveType.DisplayNames) },
			{ AttendanceAndLeave_SettingLeavePolicy.Default, new SubmoduleInfo(AttendanceAndLeave_SettingLeavePolicy.All, AttendanceAndLeave_SettingLeavePolicy.DisplayNames) },
			{ ProfessionalManagement_PerformanceReview.Default, new SubmoduleInfo(ProfessionalManagement_PerformanceReview.All, ProfessionalManagement_PerformanceReview.DisplayNames) },
			{ ProfessionalManagement_KpiSetup.Default, new SubmoduleInfo(ProfessionalManagement_KpiSetup.All, ProfessionalManagement_KpiSetup.DisplayNames) },
			{ ProfessionalManagement_ReviewHistory.Default, new SubmoduleInfo(ProfessionalManagement_ReviewHistory.All, ProfessionalManagement_ReviewHistory.DisplayNames) },
			{ Reports.Default, new SubmoduleInfo(Reports.All, Reports.DisplayNames) },
			{ Notification.Default, new SubmoduleInfo(Notification.All, Notification.DisplayNames) }
		};
	}
}
