using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using Volo.Abp;

namespace ShikshaMitra.HR.Policies;

public static class AttendancePolicyRuleValidator
{
	private const int MaxGraceMinutes = 240;
	private const int MaxLateAllowed = 31;
	private const int MaxOvertimeMinutes = 1440;

	public static void Validate(
		string name,
		HrPolicyStatus status,
		bool isDefault,
		double fullDayThresholdPercentage,
		double halfDayThresholdPercentage,
		double absentThresholdPercentage,
		int graceTime,
		int maxLateAllowedPerMonth,
		int penaltyCountThreshold,
		int earlyExitGraceTime,
		bool overtimeEnabled,
		AttendanceOvertimeRate? overtimeRate,
		int? minOvertimeMinutes)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			throw new BusinessException(HRErrorCodes.AttendancePolicyNameRequired);
		}

		if (isDefault && status != HrPolicyStatus.Active)
		{
			throw new BusinessException(HRErrorCodes.DefaultPolicyMustBeActive);
		}

		if (fullDayThresholdPercentage is < 0 or > 100 || halfDayThresholdPercentage is < 0 or > 100 || absentThresholdPercentage is < 0 or > 100)
		{
			throw new BusinessException(HRErrorCodes.InvalidAttendanceThreshold);
		}

		if (halfDayThresholdPercentage >= fullDayThresholdPercentage || absentThresholdPercentage > halfDayThresholdPercentage)
		{
			throw new BusinessException(HRErrorCodes.InvalidAttendanceThresholdOrder);
		}

		if (graceTime is < 0 or > MaxGraceMinutes ||
			maxLateAllowedPerMonth is < 0 or > MaxLateAllowed ||
			penaltyCountThreshold is < 0 or > MaxLateAllowed ||
			earlyExitGraceTime is < 0 or > MaxGraceMinutes)
		{
			throw new BusinessException(HRErrorCodes.InvalidAttendancePolicyRule);
		}

		if (overtimeEnabled && (!overtimeRate.HasValue || !minOvertimeMinutes.HasValue || minOvertimeMinutes is < 0 or > MaxOvertimeMinutes))
		{
			throw new BusinessException(HRErrorCodes.OvertimeRateAndMinMinutesRequired);
		}
	}
}
