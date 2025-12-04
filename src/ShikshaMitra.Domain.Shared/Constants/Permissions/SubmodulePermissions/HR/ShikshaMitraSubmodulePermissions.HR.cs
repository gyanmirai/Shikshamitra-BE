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
			
			public const string View = $"{SubmoduleName}.View";
			
			public static readonly string[] All = { View };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" }
			};
		}

		// Reports
		public static class Reports
		{
			private const string SubmoduleName = $"{ModuleName}.Reports";
			private const string SubmoduleDisplayName = $"{DisplayName}.Reports";
			
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

		// Notification
		public static class Notification
		{
			private const string SubmoduleName = $"{ModuleName}.Notification";
			private const string SubmoduleDisplayName = $"{DisplayName}.Notification";
			
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

		public static readonly Dictionary<string, SubmoduleInfo> Submodules = new()
		{
			{ "HR.Dashboard", new SubmoduleInfo(Dashboard.All, Dashboard.DisplayNames) },
			{ "HR.Employee.EmployeeList", new SubmoduleInfo(Employee_EmployeeList.All, Employee_EmployeeList.DisplayNames) },
			{ "HR.Employee.ICard", new SubmoduleInfo(Employee_ICard.All, Employee_ICard.DisplayNames) },
			{ "HR.Employee.Setting.DocumentTypes", new SubmoduleInfo(Employee_SettingDocumentTypes.All, Employee_SettingDocumentTypes.DisplayNames) },
			{ "HR.PayrollManagement.Dashboard", new SubmoduleInfo(PayrollManagement_Dashboard.All, PayrollManagement_Dashboard.DisplayNames) },
			{ "HR.PayrollManagement.RunPayroll", new SubmoduleInfo(PayrollManagement_RunPayroll.All, PayrollManagement_RunPayroll.DisplayNames) },
			{ "HR.PayrollManagement.GenerateSalarySlip", new SubmoduleInfo(PayrollManagement_GenerateSalarySlip.All, PayrollManagement_GenerateSalarySlip.DisplayNames) },
			{ "HR.PayrollManagement.Settings.EarningComponents", new SubmoduleInfo(PayrollManagement_SettingsEarningComponents.All, PayrollManagement_SettingsEarningComponents.DisplayNames) },
			{ "HR.PayrollManagement.Settings.DeductionComponents", new SubmoduleInfo(PayrollManagement_SettingsDeductionComponents.All, PayrollManagement_SettingsDeductionComponents.DisplayNames) },
			{ "HR.PayrollManagement.Settings.PfRule", new SubmoduleInfo(PayrollManagement_SettingsPfRule.All, PayrollManagement_SettingsPfRule.DisplayNames) },
			{ "HR.AttendanceAndLeave.ImportAttendance", new SubmoduleInfo(AttendanceAndLeave_ImportAttendance.All, AttendanceAndLeave_ImportAttendance.DisplayNames) },
			{ "HR.AttendanceAndLeave.LeaveManagement", new SubmoduleInfo(AttendanceAndLeave_LeaveManagement.All, AttendanceAndLeave_LeaveManagement.DisplayNames) },
			{ "HR.AttendanceAndLeave.AttendanceTracking", new SubmoduleInfo(AttendanceAndLeave_AttendanceTracking.All, AttendanceAndLeave_AttendanceTracking.DisplayNames) },
			{ "HR.AttendanceAndLeave.Setting.ShiftManagement", new SubmoduleInfo(AttendanceAndLeave_SettingShiftManagement.All, AttendanceAndLeave_SettingShiftManagement.DisplayNames) },
			{ "HR.AttendanceAndLeave.Setting.LeaveType", new SubmoduleInfo(AttendanceAndLeave_SettingLeaveType.All, AttendanceAndLeave_SettingLeaveType.DisplayNames) },
			{ "HR.AttendanceAndLeave.Setting.LeavePolicy", new SubmoduleInfo(AttendanceAndLeave_SettingLeavePolicy.All, AttendanceAndLeave_SettingLeavePolicy.DisplayNames) },
			{ "HR.ProfessionalManagement.PerformanceReview", new SubmoduleInfo(ProfessionalManagement_PerformanceReview.All, ProfessionalManagement_PerformanceReview.DisplayNames) },
			{ "HR.ProfessionalManagement.KpiSetup", new SubmoduleInfo(ProfessionalManagement_KpiSetup.All, ProfessionalManagement_KpiSetup.DisplayNames) },
			{ "HR.ProfessionalManagement.ReviewHistory", new SubmoduleInfo(ProfessionalManagement_ReviewHistory.All, ProfessionalManagement_ReviewHistory.DisplayNames) },
			{ "HR.Reports", new SubmoduleInfo(Reports.All, Reports.DisplayNames) },
			{ "HR.Notification", new SubmoduleInfo(Notification.All, Notification.DisplayNames) }
		};
	}
}
