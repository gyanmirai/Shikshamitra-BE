using System;
using System.Linq;

namespace ShikshaMitra.HR.Policies;

internal static class HrPolicyRequestValidation
{
	public static bool BeKnownPolicyStatus(string? value) =>
		IsOneOf(value, "draft", "active", "archived");

	public static bool BeEditablePolicyStatus(string? value) =>
		IsOneOf(value, "draft", "active");

	public static bool BeKnownPolicyType(string? value) =>
		IsKnownImpactPreviewPolicyType(value);

	public static bool IsKnownImpactPreviewPolicyType(string? value) =>
		IsOneOf(value, "workcalendar", "work_calendar", "payroll", "attendance");

	public static bool BeKnownTargetType(string? value) =>
		IsOneOf(value, "employee", "group");

	public static bool BeKnownDayCalculationType(string? value) =>
		IsOneOf(value, "working-days", "total-month-days", "manual-days");

	public static bool BeKnownSaturdayWorkPolicy(string? value) =>
		IsOneOf(value, "full-day", "half-day", "off");

	public static bool BeKnownWeeklyOffPattern(string? value) =>
		IsOneOf(value, "saturday_sunday", "sunday", "custom");

	public static bool IsCustomWeeklyOffPattern(string? value) =>
		string.Equals(NormalizePolicyToken(value), "custom", StringComparison.OrdinalIgnoreCase);

	public static bool BeKnownHolidaySyncMode(string? value) =>
		IsOneOf(value, "linked_with_overrides", "snapshot", "independent");

	public static bool BeKnownAttendanceCaptureMode(string? value) =>
		IsOneOf(value, "integrated", "manual_status", "manual_with_time");

	public static bool BeKnownMissingPunchBehavior(string? value) =>
		IsOneOf(value, "requires_review", "mark_absent", "mark_half_day");

	public static bool BeKnownAttendancePenaltyType(string? value) =>
		IsOneOf(value, "none", "warning", "salary_deduction", "deduct_half_day", "deduct_full_day");

	public static bool BeKnownAttendanceOvertimeRate(string? value) =>
		string.IsNullOrWhiteSpace(value) || IsOneOf(value, "1.5x", "2.0x", "flat");

	public static bool BeKnownAttendanceHolidayWorkBehavior(string? value) =>
		IsOneOf(value, "comp_off", "paid", "both", "ot_payout", "no_benefit");

	public static bool IsActive(string? value) =>
		string.Equals(NormalizePolicyToken(value), "active", StringComparison.OrdinalIgnoreCase);

	public static bool IsManualDays(string? value) =>
		string.Equals(NormalizePolicyToken(value), "manual-days", StringComparison.OrdinalIgnoreCase);

	private static bool IsOneOf(string? value, params string[] allowed) =>
		allowed.Contains(NormalizePolicyToken(value));

	private static string NormalizePolicyToken(string? value) =>
		(value ?? string.Empty).Trim().ToLowerInvariant();
}
