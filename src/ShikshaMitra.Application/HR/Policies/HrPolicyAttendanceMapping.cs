using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using Volo.Abp;

namespace ShikshaMitra.HR.Policies;

internal static partial class HrPolicyMapping
{
	public static AttendanceMissingPunchBehavior ParseMissingPunchBehavior(string value) => Normalize(value) switch
	{
		"mark_absent" => AttendanceMissingPunchBehavior.MarkAbsent,
		"mark_half_day" => AttendanceMissingPunchBehavior.MarkHalfDay,
		"requires_review" or "" => AttendanceMissingPunchBehavior.RequiresReview,
		_ => throw new BusinessException(HRErrorCodes.UnsupportedMissingPunchBehavior)
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
		"none" or "" => AttendancePenaltyType.None,
		_ => throw new BusinessException(HRErrorCodes.UnsupportedAttendancePenaltyType)
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
		"" => null,
		_ => throw new BusinessException(HRErrorCodes.UnsupportedAttendanceOvertimeRate)
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
		"comp_off" or "" => AttendanceHolidayWorkBehavior.CompOff,
		_ => throw new BusinessException(HRErrorCodes.UnsupportedAttendanceHolidayWorkBehavior)
	};

	public static string MapHolidayWorkBehavior(AttendanceHolidayWorkBehavior value) => value switch
	{
		AttendanceHolidayWorkBehavior.Paid => "paid",
		AttendanceHolidayWorkBehavior.OvertimePayout => "ot_payout",
		_ => "comp_off"
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
}
