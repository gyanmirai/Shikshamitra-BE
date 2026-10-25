using System;
using System.Security.Cryptography;
using System.Text;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings;

namespace ShikshaMitra.HR.Policies;

/// <summary>Editable onboarding settings, not a statutory or contractual policy catalogue.</summary>
public static class HrStarterPolicyDefaults
{
	public const string TenantEnabledProperty = "HrStarterPoliciesEnabled";
	public const string YearInitializedProperty = "HrStarterPoliciesInitialized";
	public const string ShiftInitializedProperty = "HrStarterShiftInitialized";
	public const string ReviewNotice = "Editable starter settings; review your school's contracts and applicable requirements before operating leave or payroll. No statutory entitlement or deduction is configured.";

	public static Guid RecordId(AcademicYear year, string kind) => RecordId(year.TenantId!.Value, year.Id, kind);
	public static ShiftManagement Shift(Guid tenantId) => new(RecordId(tenantId, Guid.Empty, "shift"),
		tenantId, "Starter Morning Shift (editable)", new TimeOnly(7, 0), new TimeOnly(12, 30));

	private static Guid RecordId(Guid tenantId, Guid scopeId, string kind) => new(
		SHA256.HashData(Encoding.UTF8.GetBytes($"hr-starter-v1:{tenantId:N}:{scopeId:N}:{kind}"))[..16]);

	public static WorkCalendarPolicy Calendar(Guid id, AcademicYear year) => new(
		id, year.TenantId, year.Id, "Starter School Calendar (editable)", HrPolicyStatus.Active,
		year.StartDate, year.EndDate, AttendanceWeeklyOffPattern.SaturdaySunday, [],
		WorkCalendarHolidaySyncMode.LinkedWithOverrides);

	public static PayrollPolicy Payroll(Guid id, AcademicYear year, Guid calendarId) => new(
		id, year.TenantId, year.Id, calendarId, "Starter Payroll Policy (review before payroll)",
		HrPolicyStatus.Active, true, DayCalculationType.WorkingDays, null, [], SaturdayWorkPolicy.FullDay, []);

	public static AttendancePolicy Attendance(Guid id, AcademicYear year, Guid calendarId) => new(
		id, year.TenantId, year.Id, calendarId, "Starter Attendance Policy (manual review)",
		HrPolicyStatus.Active, true, AttendanceCaptureMode.ManualStatus,
		75, 50, 0, AttendanceMissingPunchBehavior.RequiresReview, true, 5, 3,
		AttendancePenaltyType.None, 0, false, 0, false, null, null,
		AttendanceHolidayWorkBehavior.NoBenefit, false);

	public static LeavePolicy Leave(Guid id, AcademicYear year, Guid paidRuleId, Guid unpaidRuleId)
	{
		var policy = new LeavePolicy(id, year.TenantId, year.Id,
			"Starter Leave Policy (configure entitlements)", year.StartDate, year.EndDate,
			HrPolicyStatus.Active, true, ReviewNotice + " Paid and unpaid allocations are unconfigured (zero days); configure both before accepting leave requests.");
		policy.ReplaceRules([
			Rule(paidRuleId, year.TenantId, id, LeavePaidType.Paid),
			Rule(unpaidRuleId, year.TenantId, id, LeavePaidType.Unpaid)
		]);
		return policy;
	}

	private static LeavePolicyRule Rule(Guid id, Guid? tenantId, Guid policyId, LeavePaidType paidType) => new(
		id, tenantId, policyId, paidType, LeaveEntitlementMode.Limited, LeaveAccrualPolicyType.Yearly,
		0, false, null, false, null, true, 0, 30, true, null, false);
}
