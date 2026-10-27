using System;
using System.Linq;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using Volo.Abp;

namespace ShikshaMitra.HR.Policies;

internal static class HrSetupReviewValidity
{
	// Validate persisted legacy configuration with the owning domain invariants, without saving it.
	public static bool Calendar(WorkCalendarPolicy x) => Valid(() => new WorkCalendarPolicy(x.Id, x.TenantId,
		x.AcademicYearId, x.Name, x.Status, x.EffectiveFrom, x.EffectiveTo, x.WeeklyOffPattern,
		x.GetCustomWeeklyOffs(), x.HolidaySyncMode));
	public static bool Payroll(PayrollPolicy x) => Valid(() => new PayrollPolicy(x.Id, x.TenantId,
		x.AcademicYearId, x.WorkCalendarPolicyId, x.Name, x.Status, x.IsDefault, x.DayCalculationType,
		x.ManualDays, x.GetManualMonthDays(), x.SaturdayWorkPolicy, x.GetHolidayOverrides(), x.GetLockedMonthSettings()));
	public static bool Attendance(AttendancePolicy x) => Valid(() => new AttendancePolicy(x.Id, x.TenantId,
		x.AcademicYearId, x.WorkCalendarPolicyId, x.Name, x.Status, x.IsDefault, x.CaptureMode,
		x.FullDayThresholdPercentage, x.HalfDayThresholdPercentage, x.AbsentThresholdPercentage, x.MissingPunchBehavior,
		x.ManualCorrectionAllowed, x.GraceTime, x.MaxLateAllowedPerMonth, x.PenaltyType, x.PenaltyCountThreshold,
		x.EarlyExitEnabled, x.EarlyExitGraceTime, x.OvertimeEnabled, x.OvertimeRate, x.MinOvertimeMinutes,
		x.HolidayWorkBehavior, x.PayrollPreviewEnabled, x.PenaltyDeductionAmount));
	public static bool Leave(LeavePolicy x) => (!x.EffectiveTo.HasValue || x.EffectiveTo >= x.EffectiveFrom) &&
		x.Rules.Count(r => r.IsActive && r.PaidType == LeavePaidType.Paid) == 1 &&
		x.Rules.Count(r => r.IsActive && r.PaidType == LeavePaidType.Unpaid) == 1 &&
		x.Rules.Where(r => r.IsActive).All(r => r.IsUnlimited || r.LeaveCount is >= 0);

	private static bool Valid<T>(Func<T> validate)
	{
		try { validate(); return true; }
		catch (BusinessException) { return false; }
	}
}
