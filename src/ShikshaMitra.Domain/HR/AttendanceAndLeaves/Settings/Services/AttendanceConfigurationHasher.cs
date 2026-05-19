using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public static class AttendanceConfigurationHasher
{
	public static string Compute(AttendanceConfiguration configuration)
	{
		var payload = new
		{
			configuration.PolicyName,
			configuration.Status,
			configuration.EffectiveFrom,
			configuration.EffectiveTo,
			configuration.WeeklyOffPattern,
			configuration.CustomWeeklyOffs,
			configuration.HoursBasedPayPercentage,
			configuration.HalfDayPayPercentage,
			configuration.AbsentPayPercentage,
			configuration.MissingPunchBehavior,
			configuration.ManualCorrectionAllowed,
			configuration.GraceTime,
			configuration.MaxLateAllowedPerMonth,
			configuration.PenaltyType,
			configuration.PenaltyCountThreshold,
			configuration.EarlyExitEnabled,
			configuration.EarlyExitGraceTime,
			configuration.OvertimeEnabled,
			configuration.OvertimeRate,
			configuration.MinOvertimeMinutes,
			configuration.HolidayWorkBehavior,
			configuration.SandwichRuleEnabled,
			configuration.PayrollIntegrationMode,
			configuration.RequirePublishedAttendanceForPayrollLock,
			Overrides = configuration.Overrides
				.OrderBy(x => x.TargetType)
				.ThenBy(x => x.TargetId, StringComparer.Ordinal)
				.Select(x => new
				{
					x.TargetType,
					x.TargetId,
					x.IsActive,
					x.HoursBasedPayPercentage,
					x.GraceTime,
					x.MaxLateAllowedPerMonth,
					x.PenaltyType,
					x.OvertimeEnabled,
					x.OvertimeRate,
					x.MinOvertimeMinutes
				})
		};

		return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload))));
	}
}
