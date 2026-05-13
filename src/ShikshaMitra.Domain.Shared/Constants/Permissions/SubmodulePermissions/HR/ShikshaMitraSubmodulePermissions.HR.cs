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
			{ "HR.PayrollManagement", new SubmoduleInfo(PayrollManagement.All, PayrollManagement.DisplayNames) },
			{ "HR.PayrollManagement.Overview", new SubmoduleInfo(PayrollManagement_Overview.All, PayrollManagement_Overview.DisplayNames) },
			{ "HR.PayrollManagement.Dashboard", new SubmoduleInfo(PayrollManagement_Dashboard.All, PayrollManagement_Dashboard.DisplayNames) },
			{ "HR.PayrollManagement.RunPayroll", new SubmoduleInfo(PayrollManagement_RunPayroll.All, PayrollManagement_RunPayroll.DisplayNames) },
			{ "HR.PayrollManagement.GenerateSalarySlip", new SubmoduleInfo(PayrollManagement_GenerateSalarySlip.All, PayrollManagement_GenerateSalarySlip.DisplayNames) },
			{ "HR.PayrollManagement.PayrollStructure", new SubmoduleInfo(PayrollManagement_PayrollStructure.All, PayrollManagement_PayrollStructure.DisplayNames) },
			{ "HR.PayrollManagement.Settings.EarningComponents", new SubmoduleInfo(PayrollManagement_SettingsEarningComponents.All, PayrollManagement_SettingsEarningComponents.DisplayNames) },
			{ "HR.PayrollManagement.Settings.DeductionComponents", new SubmoduleInfo(PayrollManagement_SettingsDeductionComponents.All, PayrollManagement_SettingsDeductionComponents.DisplayNames) },
			{ "HR.PayrollManagement.Settings.PfRule", new SubmoduleInfo(PayrollManagement_SettingsPfRule.All, PayrollManagement_SettingsPfRule.DisplayNames) },
			{ "HR.PayrollManagement.Settings.PayrollConfiguration", new SubmoduleInfo(PayrollManagement_SettingsPayrollConfiguration.All, PayrollManagement_SettingsPayrollConfiguration.DisplayNames) },
			{ "HR.PayrollManagement.Settings.Tds", new SubmoduleInfo(PayrollManagement_SettingsTds.All, PayrollManagement_SettingsTds.DisplayNames) },
			{ "HR.PayrollManagement.Settings.ProfessionalTax", new SubmoduleInfo(PayrollManagement_SettingsProfessionalTax.All, PayrollManagement_SettingsProfessionalTax.DisplayNames) },
			{ "HR.AttendanceAndLeave.Dashboard", new SubmoduleInfo(AttendanceAndLeave_Dashboard.All, AttendanceAndLeave_Dashboard.DisplayNames) },
			{ "HR.AttendanceAndLeave.MarkAttendance", new SubmoduleInfo(AttendanceAndLeave_MarkAttendance.All, AttendanceAndLeave_MarkAttendance.DisplayNames) },
			{ "HR.AttendanceAndLeave.BiometricAttendance", new SubmoduleInfo(AttendanceAndLeave_BiometricAttendance.All, AttendanceAndLeave_BiometricAttendance.DisplayNames) },
			{ "HR.AttendanceAndLeave.Setting.AttendanceConfiguration", new SubmoduleInfo(AttendanceAndLeave_SettingAttendanceConfiguration.All, AttendanceAndLeave_SettingAttendanceConfiguration.DisplayNames) },
			{ "HR.LeaveManagement.LeaveCenter", new SubmoduleInfo(LeaveManagement_LeaveCenter.All, LeaveManagement_LeaveCenter.DisplayNames) },
			{ "HR.LeaveManagement.Setting.LeaveType", new SubmoduleInfo(LeaveManagement_SettingLeaveType.All, LeaveManagement_SettingLeaveType.DisplayNames) },
			{ "HR.LeaveManagement.Setting.LeavePolicy", new SubmoduleInfo(LeaveManagement_SettingLeavePolicy.All, LeaveManagement_SettingLeavePolicy.DisplayNames) },
			{ "HR.ProfessionalManagement", new SubmoduleInfo(ProfessionalManagement.All, ProfessionalManagement.DisplayNames) },
			{ "HR.Reports", new SubmoduleInfo(Reports.All, Reports.DisplayNames) },
			{ "HR.Notification", new SubmoduleInfo(Notification.All, Notification.DisplayNames) }
		};
	}
}
