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
			{ "HR.PayrollManagement.RunPayroll", new SubmoduleInfo(PayrollManagement_RunPayroll.All, PayrollManagement_RunPayroll.DisplayNames) },
			{ "HR.PayrollManagement.GenerateSalarySlip", new SubmoduleInfo(PayrollManagement_GenerateSalarySlip.All, PayrollManagement_GenerateSalarySlip.DisplayNames) },
			{ "HR.PayrollManagement.Settings.EarningComponents", new SubmoduleInfo(PayrollManagement_SettingsEarningComponents.All, PayrollManagement_SettingsEarningComponents.DisplayNames) },
			{ "HR.PayrollManagement.Settings.DeductionComponents", new SubmoduleInfo(PayrollManagement_SettingsDeductionComponents.All, PayrollManagement_SettingsDeductionComponents.DisplayNames) },
			{ "HR.PayrollManagement.Settings.PfRule", new SubmoduleInfo(PayrollManagement_SettingsPfRule.All, PayrollManagement_SettingsPfRule.DisplayNames) },
			{ "HR.PayrollManagement.Settings.PayrollConfiguration", new SubmoduleInfo(PayrollManagement_SettingsPayrollConfiguration.All, PayrollManagement_SettingsPayrollConfiguration.DisplayNames) },
			{ "HR.AttendanceAndLeave.MarkAttendance", new SubmoduleInfo(AttendanceAndLeave_MarkAttendance.All, AttendanceAndLeave_MarkAttendance.DisplayNames) },
			{ "HR.AttendanceAndLeave.BiometricAttendance", new SubmoduleInfo(AttendanceAndLeave_BiometricAttendance.All, AttendanceAndLeave_BiometricAttendance.DisplayNames) },
			{ "HR.AttendanceAndLeave.Setting.ShiftManagement", new SubmoduleInfo(AttendanceAndLeave_SettingShiftManagement.All, AttendanceAndLeave_SettingShiftManagement.DisplayNames) },
			{ "HR.AttendanceAndLeave.Setting.AttendanceConfiguration", new SubmoduleInfo(AttendanceAndLeave_SettingAttendanceConfiguration.All, AttendanceAndLeave_SettingAttendanceConfiguration.DisplayNames) },
			{ "HR.LeaveManagement.ApplyLeave", new SubmoduleInfo(LeaveManagement_ApplyLeave.All, LeaveManagement_ApplyLeave.DisplayNames) },
			{ "HR.LeaveManagement.Approvals", new SubmoduleInfo(LeaveManagement_Approvals.All, LeaveManagement_Approvals.DisplayNames) },
			{ "HR.LeaveManagement.LeaveBalance", new SubmoduleInfo(LeaveManagement_LeaveBalance.All, LeaveManagement_LeaveBalance.DisplayNames) },
			{ "HR.LeaveManagement.LeavePolicySetup", new SubmoduleInfo(LeaveManagement_LeavePolicySetup.All, LeaveManagement_LeavePolicySetup.DisplayNames) },
			{ "HR.LeaveManagement.Setting.LeaveType", new SubmoduleInfo(LeaveManagement_SettingLeaveType.All, LeaveManagement_SettingLeaveType.DisplayNames) },
			{ "HR.LeaveManagement.Setting.LeavePolicy", new SubmoduleInfo(LeaveManagement_SettingLeavePolicy.All, LeaveManagement_SettingLeavePolicy.DisplayNames) },
			{ "HR.ProfessionalManagement.PerformanceReview", new SubmoduleInfo(ProfessionalManagement_PerformanceReview.All, ProfessionalManagement_PerformanceReview.DisplayNames) },
			{ "HR.ProfessionalManagement.KpiSetup", new SubmoduleInfo(ProfessionalManagement_KpiSetup.All, ProfessionalManagement_KpiSetup.DisplayNames) },
			{ "HR.ProfessionalManagement.ReviewHistory", new SubmoduleInfo(ProfessionalManagement_ReviewHistory.All, ProfessionalManagement_ReviewHistory.DisplayNames) },
			{ "HR.Reports", new SubmoduleInfo(Reports.All, Reports.DisplayNames) },
			{ "HR.Notification", new SubmoduleInfo(Notification.All, Notification.DisplayNames) }
		};
	}
}
