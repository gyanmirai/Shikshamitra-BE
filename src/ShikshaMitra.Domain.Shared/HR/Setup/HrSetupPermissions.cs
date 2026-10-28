using ShikshaMitra.Constants.Permissions;

namespace ShikshaMitra.HR.Setup;

/// <summary>Existing operator actions, deliberately excluding employee self-service.</summary>
public static class HrSetupPermissions
{
	public static readonly string[] Operators =
	[
		ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add,
		ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Edit,
		ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeOnboarding.Add,
		ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeOnboarding.Edit,
		ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeOnboarding.Finalize,
		ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_MarkAttendance.Add,
		ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.Add,
		ShikshaMitraSubmodulePermissions.HR.PayrollManagement_PayrollStructure.Add,
		ShikshaMitraSubmodulePermissions.HR.PayrollManagement_GenerateSalarySlip.Add,
		ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.Lock,
		ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.Adjustments,
		ShikshaMitraSubmodulePermissions.HR.PayrollManagement_CompensationRevisions.Manage,
		ShikshaMitraSubmodulePermissions.HR.Policies.Add,
		ShikshaMitraSubmodulePermissions.HR.Policies.Edit,
		ShikshaMitraSubmodulePermissions.HR.Policies.SetDefault,
		ShikshaMitraSubmodulePermissions.HR.LeaveManagement_SettingLeavePolicy.Add,
		ShikshaMitraSubmodulePermissions.HR.LeaveManagement_SettingLeavePolicy.Edit,
		ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_Setting_ShiftManagement.Add,
		ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsEarningComponents.Add,
		ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsDeductionComponents.Add,
		ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsPfRule.Add,
		ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsProfessionalTax.Add,
		ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsTds.Add,
		ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.Add,
		ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.Edit,
		ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsStatutorySchemes.Edit
	];
}
