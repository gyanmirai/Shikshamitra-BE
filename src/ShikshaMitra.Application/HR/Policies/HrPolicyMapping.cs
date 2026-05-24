using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Payroll.Settings;
using Volo.Abp;

namespace ShikshaMitra.HR.Policies;

internal static class HrPolicyMapping
{
	public static HrPolicyStatus ParsePolicyStatus(string value) => Normalize(value) switch
	{
		"active" => HrPolicyStatus.Active,
		"archived" => HrPolicyStatus.Archived,
		_ => HrPolicyStatus.Draft
	};

	public static string MapPolicyStatus(HrPolicyStatus value) => value switch
	{
		HrPolicyStatus.Active => "active",
		HrPolicyStatus.Archived => "archived",
		_ => "draft"
	};

	public static HrPolicyAssignmentTargetType ParseAssignmentTargetType(string value) => Normalize(value).Replace("-", "_") switch
	{
		"employee" or "" => HrPolicyAssignmentTargetType.Employee,
		"group" => HrPolicyAssignmentTargetType.Group,
		_ => throw new BusinessException("ShikshaMitra:PolicyAssignmentTargetTypeUnsupported")
	};

	public static string MapAssignmentTargetType(HrPolicyAssignmentTargetType value) => value switch
	{
		HrPolicyAssignmentTargetType.Group => "group",
		_ => "employee"
	};

	public static WorkCalendarHolidaySyncMode ParseHolidaySyncMode(string value) => Normalize(value) switch
	{
		"snapshot" => WorkCalendarHolidaySyncMode.Snapshot,
		"independent" => WorkCalendarHolidaySyncMode.Independent,
		_ => WorkCalendarHolidaySyncMode.LinkedWithOverrides
	};

	public static string MapHolidaySyncMode(WorkCalendarHolidaySyncMode value) => value switch
	{
		WorkCalendarHolidaySyncMode.Snapshot => "snapshot",
		WorkCalendarHolidaySyncMode.Independent => "independent",
		_ => "linked_with_overrides"
	};

	public static WorkCalendarHolidaySource ParseWorkCalendarHolidaySource(string? value) => Normalize(value) switch
	{
		"manual" => WorkCalendarHolidaySource.Manual,
		_ => WorkCalendarHolidaySource.CentralCalendar
	};

	public static string MapWorkCalendarHolidaySource(WorkCalendarHolidaySource value) => value switch
	{
		WorkCalendarHolidaySource.Manual => "manual",
		_ => "central_calendar"
	};

	public static AttendanceWeeklyOffPattern ParseWeeklyOffPattern(string value) => Normalize(value) switch
	{
		"sunday" => AttendanceWeeklyOffPattern.Sunday,
		"custom" => AttendanceWeeklyOffPattern.Custom,
		_ => AttendanceWeeklyOffPattern.SaturdaySunday
	};

	public static string MapWeeklyOffPattern(AttendanceWeeklyOffPattern value) => value switch
	{
		AttendanceWeeklyOffPattern.Sunday => "sunday",
		AttendanceWeeklyOffPattern.Custom => "custom",
		_ => "saturday_sunday"
	};

	public static DayCalculationType ParseDayCalculationType(string value) => Normalize(value) switch
	{
		"total-month-days" => DayCalculationType.TotalMonthDays,
		"manual-days" => DayCalculationType.ManualDays,
		_ => DayCalculationType.WorkingDays
	};

	public static string MapDayCalculationType(DayCalculationType value) => value switch
	{
		DayCalculationType.TotalMonthDays => "total-month-days",
		DayCalculationType.ManualDays => "manual-days",
		_ => "working-days"
	};

	public static SaturdayWorkPolicy ParseSaturdayWorkPolicy(string value) => Normalize(value) switch
	{
		"half-day" => SaturdayWorkPolicy.HalfDay,
		"off" => SaturdayWorkPolicy.Off,
		_ => SaturdayWorkPolicy.FullDay
	};

	public static string MapSaturdayWorkPolicy(SaturdayWorkPolicy value) => value switch
	{
		SaturdayWorkPolicy.HalfDay => "half-day",
		SaturdayWorkPolicy.Off => "off",
		_ => "full-day"
	};

	public static AttendanceMissingPunchBehavior ParseMissingPunchBehavior(string value) => Normalize(value) switch
	{
		"mark_absent" => AttendanceMissingPunchBehavior.MarkAbsent,
		"mark_half_day" => AttendanceMissingPunchBehavior.MarkHalfDay,
		_ => AttendanceMissingPunchBehavior.RequiresReview
	};

	public static string MapMissingPunchBehavior(AttendanceMissingPunchBehavior value) => value switch
	{
		AttendanceMissingPunchBehavior.MarkAbsent => "mark_absent",
		AttendanceMissingPunchBehavior.MarkHalfDay => "mark_half_day",
		_ => "requires_review"
	};

	public static AttendancePenaltyType ParsePenaltyType(string value) => Normalize(value) switch
	{
		"deduct_half_day" => AttendancePenaltyType.DeductHalfDay,
		"deduct_full_day" => AttendancePenaltyType.DeductFullDay,
		_ => AttendancePenaltyType.None
	};

	public static string MapPenaltyType(AttendancePenaltyType value) => value switch
	{
		AttendancePenaltyType.DeductHalfDay => "deduct_half_day",
		AttendancePenaltyType.DeductFullDay => "deduct_full_day",
		_ => "none"
	};

	public static AttendanceOvertimeRate? ParseOvertimeRate(string? value) => Normalize(value) switch
	{
		"1.5x" => AttendanceOvertimeRate.OnePointFiveX,
		"2.0x" => AttendanceOvertimeRate.TwoX,
		"flat" => AttendanceOvertimeRate.Flat,
		_ => null
	};

	public static string? MapOvertimeRate(AttendanceOvertimeRate? value) => value switch
	{
		AttendanceOvertimeRate.OnePointFiveX => "1.5x",
		AttendanceOvertimeRate.TwoX => "2.0x",
		AttendanceOvertimeRate.Flat => "flat",
		_ => null
	};

	public static AttendanceHolidayWorkBehavior ParseHolidayWorkBehavior(string value) => Normalize(value) switch
	{
		"paid" => AttendanceHolidayWorkBehavior.Paid,
		"ot_payout" => AttendanceHolidayWorkBehavior.OvertimePayout,
		_ => AttendanceHolidayWorkBehavior.CompOff
	};

	public static string MapHolidayWorkBehavior(AttendanceHolidayWorkBehavior value) => value switch
	{
		AttendanceHolidayWorkBehavior.Paid => "paid",
		AttendanceHolidayWorkBehavior.OvertimePayout => "ot_payout",
		_ => "comp_off"
	};

	public static WorkCalendarPolicyDto ToDto(WorkCalendarPolicy entity) => new()
	{
		Id = entity.Id,
		AcademicYearId = entity.AcademicYearId,
		Name = entity.Name,
		Status = MapPolicyStatus(entity.Status),
		EffectiveFrom = entity.EffectiveFrom,
		EffectiveTo = entity.EffectiveTo,
		WeeklyOffPattern = MapWeeklyOffPattern(entity.WeeklyOffPattern),
		CustomWeeklyOffs = entity.GetCustomWeeklyOffs().ToList(),
		HolidaySyncMode = MapHolidaySyncMode(entity.HolidaySyncMode),
		Version = entity.Version,
		CreationTime = entity.CreationTime,
		CreatorId = entity.CreatorId,
		LastModificationTime = entity.LastModificationTime,
		LastModifierId = entity.LastModifierId
	};

	public static WorkCalendarHolidayDto ToDto(WorkCalendarHoliday entity) => new()
	{
		Id = entity.Id,
		AcademicYearId = entity.AcademicYearId,
		WorkCalendarPolicyId = entity.WorkCalendarPolicyId,
		Date = entity.Date,
		FromDate = entity.FromDate,
		ToDate = entity.ToDate,
		DayCount = (entity.ToDate.Date - entity.FromDate.Date).Days + 1,
		HolidayName = entity.HolidayName,
		Source = MapWorkCalendarHolidaySource(entity.Source),
		CalendarHolidayId = entity.CalendarHolidayId,
		IsIncluded = entity.IsIncluded,
		Notes = entity.Notes
	};

	public static WorkCalendarHolidaySummaryDto BuildHolidaySummary(IEnumerable<WorkCalendarHolidayDto> holidays)
	{
		var rows = holidays.ToList();
		return new WorkCalendarHolidaySummaryDto
		{
			TotalCount = rows.Count,
			IncludedCount = rows.Count(x => x.IsIncluded),
			RemovedCount = rows.Count(x => !x.IsIncluded),
			ManualCount = rows.Count(x => x.Source == "manual"),
			CentralCount = rows.Count(x => x.Source == "central_calendar")
		};
	}

	public static PayrollPolicyDto ToDto(PayrollPolicy entity) => new()
	{
		Id = entity.Id,
		AcademicYearId = entity.AcademicYearId,
		WorkCalendarPolicyId = entity.WorkCalendarPolicyId,
		Name = entity.Name,
		Status = MapPolicyStatus(entity.Status),
		IsDefault = entity.IsDefault,
		DayCalculationType = MapDayCalculationType(entity.DayCalculationType),
		ManualDays = entity.ManualDays,
		ManualMonthDays = entity.GetManualMonthDays().Select(x => new PayrollPolicyManualMonthDto
		{
			Year = x.Year,
			Month = x.Month,
			ManualDays = x.ManualDays
		}).ToList(),
		SaturdayWorkPolicy = MapSaturdayWorkPolicy(entity.SaturdayWorkPolicy),
		HolidayOverrides = entity.GetHolidayOverrides().Select(x => new PayrollPolicyHolidayOverrideDto
		{
			Date = x.Date,
			HolidayName = x.HolidayName,
			IncludedInPayroll = x.IncludedInPayroll,
			CalendarHolidayId = x.CalendarHolidayId
		}).ToList(),
		Version = entity.Version,
		CreationTime = entity.CreationTime,
		CreatorId = entity.CreatorId,
		LastModificationTime = entity.LastModificationTime,
		LastModifierId = entity.LastModifierId
	};

	public static AttendancePolicyDto ToDto(AttendancePolicy entity) => new()
	{
		Id = entity.Id,
		AcademicYearId = entity.AcademicYearId,
		WorkCalendarPolicyId = entity.WorkCalendarPolicyId,
		Name = entity.Name,
		Status = MapPolicyStatus(entity.Status),
		IsDefault = entity.IsDefault,
		FullDayThresholdPercentage = entity.FullDayThresholdPercentage,
		HalfDayThresholdPercentage = entity.HalfDayThresholdPercentage,
		AbsentThresholdPercentage = entity.AbsentThresholdPercentage,
		MissingPunchBehavior = MapMissingPunchBehavior(entity.MissingPunchBehavior),
		ManualCorrectionAllowed = entity.ManualCorrectionAllowed,
		GraceTime = entity.GraceTime,
		MaxLateAllowedPerMonth = entity.MaxLateAllowedPerMonth,
		PenaltyType = MapPenaltyType(entity.PenaltyType),
		PenaltyCountThreshold = entity.PenaltyCountThreshold,
		EarlyExitEnabled = entity.EarlyExitEnabled,
		EarlyExitGraceTime = entity.EarlyExitGraceTime,
		OvertimeEnabled = entity.OvertimeEnabled,
		OvertimeRate = MapOvertimeRate(entity.OvertimeRate),
		MinOvertimeMinutes = entity.MinOvertimeMinutes,
		HolidayWorkBehavior = MapHolidayWorkBehavior(entity.HolidayWorkBehavior),
		SandwichRuleEnabled = entity.SandwichRuleEnabled,
		PayrollPreviewEnabled = entity.PayrollPreviewEnabled,
		Version = entity.Version,
		CreationTime = entity.CreationTime,
		CreatorId = entity.CreatorId,
		LastModificationTime = entity.LastModificationTime,
		LastModifierId = entity.LastModifierId
	};

	public static HrPolicyAssignmentDto ToDto(HrPolicyAssignment entity) => new()
	{
		Id = entity.Id,
		AcademicYearId = entity.AcademicYearId,
		TargetType = MapAssignmentTargetType(entity.TargetType),
		TargetKey = entity.TargetKey,
		TargetDisplayName = entity.TargetDisplayName,
		AttendancePolicyId = entity.AttendancePolicyId,
		PayrollPolicyId = entity.PayrollPolicyId,
		EffectiveFrom = entity.EffectiveFrom,
		EffectiveTo = entity.EffectiveTo,
		IsActive = entity.IsActive,
		Priority = entity.Priority,
		AssignmentReason = entity.AssignmentReason,
		CreationTime = entity.CreationTime,
		CreatorId = entity.CreatorId,
		LastModificationTime = entity.LastModificationTime,
		LastModifierId = entity.LastModifierId
	};

	public static IReadOnlyList<DayCalculationManualMonth> ToManualMonths(IEnumerable<PayrollPolicyManualMonthDto> input) =>
		input.Select(x => new DayCalculationManualMonth(x.Year, x.Month, x.ManualDays)).ToList();

	public static IReadOnlyList<PayrollHolidayOverride> ToHolidayOverrides(IEnumerable<PayrollPolicyHolidayOverrideDto> input) =>
		input.Select(x => new PayrollHolidayOverride(x.Date.Date, x.HolidayName, x.IncludedInPayroll, x.CalendarHolidayId)).ToList();

	public static void EnsureActive(HrPolicyStatus status)
	{
		if (status != HrPolicyStatus.Active)
		{
			throw new BusinessException("ShikshaMitra:PolicyMustBeActive");
		}
	}

	private static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();
}
