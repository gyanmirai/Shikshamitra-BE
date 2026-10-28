using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using H = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.HR;

namespace ShikshaMitra.HR.Setup;

public class HrSetupPolicyReadinessReader(
	IRepository<WorkCalendarPolicy, Guid> calendars, IRepository<PayrollPolicy, Guid> payroll,
	IRepository<AttendancePolicy, Guid> attendance, ILeavePolicyRepository leave,
	IPermissionChecker permissions) : ITransientDependency
{
	public async Task<List<HrSetupCheckDto>> ReadAsync(AcademicYear? year, Guid tenantId)
	{
		var result = new List<HrSetupCheckDto>();
		var canCreate = await permissions.IsGrantedAsync(H.Policies.Add);
		var canEdit = await permissions.IsGrantedAsync(H.Policies.Edit);
		var canDefault = await permissions.IsGrantedAsync(H.Policies.SetDefault);
		var canView = await permissions.IsGrantedAsync(H.Policies.View);
		if (year == null)
			return new[] { "calendar", "attendance-policy", "leave-policy", "payroll-policy" }.Select(code => new HrSetupCheckDto
				{ Code = code, Step = "policies", State = "unknown", Reason = "Select an academic year first." }).ToList();
		var calendarRows = await calendars.GetListAsync(x => x.TenantId == tenantId && x.AcademicYearId == year.Id);
		bool CalendarUsable(WorkCalendarPolicy x) => x.Status == HrPolicyStatus.Active && HrSetupReviewValidity.Calendar(x) && x.EffectiveFrom <= year.EndDate && (!x.EffectiveTo.HasValue || x.EffectiveTo >= year.StartDate);
		var usableCalendarIds = calendarRows.Where(CalendarUsable).Select(x => x.Id).ToHashSet();
		result.Add(Check("calendar", calendarRows.Count == 0 ? "missing" : usableCalendarIds.Count > 0 ? "ready" : calendarRows.All(x => x.Status != HrPolicyStatus.Active) ? "inactive" : "invalid",
			"Create or activate a valid work calendar for this academic year.", canView && (canCreate || canEdit)));
		var payrollRows = await payroll.GetListAsync(x => x.TenantId == tenantId && x.AcademicYearId == year.Id);
		var payrollDefaults = payrollRows.Where(x => x.IsDefault && x.Status == HrPolicyStatus.Active).ToList();
		result.Add(Check("payroll-policy", DefaultState(payrollRows.Any(x => x.IsDefault), payrollDefaults.Count,
			payrollDefaults.All(x => usableCalendarIds.Contains(x.WorkCalendarPolicyId) && HrSetupReviewValidity.Payroll(x))),
			"Choose exactly one active, valid default payroll policy linked to a usable calendar.", canView && (canCreate || canEdit || canDefault)));
		var attendanceRows = await attendance.GetListAsync(x => x.TenantId == tenantId && x.AcademicYearId == year.Id);
		var attendanceDefaults = attendanceRows.Where(x => x.IsDefault && x.Status == HrPolicyStatus.Active).ToList();
		result.Add(Check("attendance-policy", DefaultState(attendanceRows.Any(x => x.IsDefault), attendanceDefaults.Count,
			attendanceDefaults.All(x => usableCalendarIds.Contains(x.WorkCalendarPolicyId) && HrSetupReviewValidity.Attendance(x))),
			"Choose exactly one active, valid default attendance policy linked to a usable calendar.", canView && (canCreate || canEdit || canDefault)));
		var leaveRows = (await leave.GetListByAcademicYearIdAsync(year.Id, includeInactive: true)).Where(x => x.TenantId == tenantId).ToList();
		var leaveDefaults = leaveRows.Where(x => x.IsDefault && x.IsActive && x.Status == HrPolicyStatus.Active).ToList();
		result.Add(Check("leave-policy", DefaultState(leaveRows.Any(x => x.IsDefault), leaveDefaults.Count,
			leaveDefaults.All(x => HrSetupReviewValidity.Leave(x) && x.EffectiveFrom <= year.EndDate && (!x.EffectiveTo.HasValue || x.EffectiveTo >= year.StartDate))),
			"Choose exactly one active, valid default leave policy for this academic year.",
			await permissions.IsGrantedAsync(H.LeaveManagement_SettingLeavePolicy.View) && (await permissions.IsGrantedAsync(H.LeaveManagement_SettingLeavePolicy.Add) || await permissions.IsGrantedAsync(H.LeaveManagement_SettingLeavePolicy.Edit))));
		return result;
	}

	private static string DefaultState(bool hasDefault, int activeCount, bool valid) => activeCount > 1 ? "conflicting" : activeCount == 0 ? hasDefault ? "inactive" : "missing" : valid ? "ready" : "invalid";
	private static HrSetupCheckDto Check(string code, string state, string reason, bool canConfigure) => new()
		{ Code = code, Step = "policies", State = state, Reason = state == "ready" ? "Usable configuration is available." : reason, CanConfigure = canConfigure };
}
