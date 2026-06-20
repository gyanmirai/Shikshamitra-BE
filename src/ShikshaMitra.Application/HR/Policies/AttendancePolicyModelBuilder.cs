using System;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;

namespace ShikshaMitra.HR.Policies;

internal static class AttendancePolicyModelBuilder
{
	public static AttendancePolicy Build(Guid id, Guid? tenantId, CreateUpdateAttendancePolicyDto input) =>
		new(
			id,
			tenantId,
			input.AcademicYearId,
			input.WorkCalendarPolicyId,
			input.Name,
			HrPolicyMapping.ParsePolicyStatus(input.Status),
			input.IsDefault,
			HrPolicyMapping.ParseCaptureMode(input.CaptureMode),
			input.FullDayThresholdPercentage,
			input.HalfDayThresholdPercentage,
			input.AbsentThresholdPercentage,
			HrPolicyMapping.ParseMissingPunchBehavior(input.MissingPunchBehavior),
			input.ManualCorrectionAllowed,
			input.GraceTime,
			input.MaxLateAllowedPerMonth,
			HrPolicyMapping.ParsePenaltyType(input.PenaltyType),
			input.PenaltyCountThreshold,
			input.EarlyExitEnabled,
			input.EarlyExitGraceTime,
			input.OvertimeEnabled,
			HrPolicyMapping.ParseOvertimeRate(input.OvertimeRate),
			input.MinOvertimeMinutes,
			HrPolicyMapping.ParseHolidayWorkBehavior(input.HolidayWorkBehavior),
			input.PayrollPreviewEnabled,
			input.PenaltyDeductionAmount);

	public static bool IsUnchanged(
		AttendancePolicy entity,
		CreateUpdateAttendancePolicyDto input,
		HrPolicyStatus status,
		AttendanceCaptureMode captureMode,
		AttendanceMissingPunchBehavior missingPunchBehavior,
		AttendancePenaltyType penaltyType,
		AttendanceOvertimeRate? overtimeRate,
		AttendanceHolidayWorkBehavior holidayWorkBehavior)
	{
		return entity.AcademicYearId == input.AcademicYearId &&
			entity.WorkCalendarPolicyId == input.WorkCalendarPolicyId &&
			string.Equals(entity.Name, input.Name.Trim(), StringComparison.Ordinal) &&
			entity.Status == status &&
			entity.IsDefault == input.IsDefault &&
			entity.CaptureMode == captureMode &&
			entity.FullDayThresholdPercentage == input.FullDayThresholdPercentage &&
			entity.HalfDayThresholdPercentage == input.HalfDayThresholdPercentage &&
			entity.AbsentThresholdPercentage == input.AbsentThresholdPercentage &&
			entity.MissingPunchBehavior == missingPunchBehavior &&
			entity.ManualCorrectionAllowed == input.ManualCorrectionAllowed &&
			entity.GraceTime == input.GraceTime &&
			entity.MaxLateAllowedPerMonth == input.MaxLateAllowedPerMonth &&
			entity.PenaltyType == penaltyType &&
			entity.PenaltyCountThreshold == input.PenaltyCountThreshold &&
			entity.PenaltyDeductionAmount == (penaltyType == AttendancePenaltyType.SalaryDeduction
				? input.PenaltyDeductionAmount
				: null) &&
			entity.EarlyExitEnabled == input.EarlyExitEnabled &&
			entity.EarlyExitGraceTime == input.EarlyExitGraceTime &&
			entity.OvertimeEnabled == input.OvertimeEnabled &&
			entity.OvertimeRate == overtimeRate &&
			entity.MinOvertimeMinutes == input.MinOvertimeMinutes &&
			entity.HolidayWorkBehavior == holidayWorkBehavior &&
			entity.PayrollPreviewEnabled == input.PayrollPreviewEnabled;
	}
}
