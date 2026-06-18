using System;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public static class LeaveAccrualCalculator
{
	public static decimal? CalculateAccruedDays(
		LeavePolicy policy,
		LeavePolicyRule rule,
		DateTime academicYearStart,
		DateTime academicYearEnd,
		DateOnly? employeeDateOfJoining,
		DateTime evaluationDate)
	{
		if (rule.IsUnlimited)
		{
			return null;
		}

		var annualEntitlement = rule.LeaveCount ?? 0;
		if (annualEntitlement <= 0)
		{
			return 0m;
		}

		var academicStart = DateOnly.FromDateTime(academicYearStart.Date);
		var academicEnd = DateOnly.FromDateTime(academicYearEnd.Date);
		if (academicEnd < academicStart)
		{
			return 0m;
		}

		var effectiveStart = DateOnly.FromDateTime(policy.EffectiveFrom.Date);
		var employeeStart = employeeDateOfJoining ?? academicStart;
		var eligibleStart = Max(academicStart, effectiveStart, employeeStart);
		var eligibleEnd = Min(
			academicEnd,
			policy.EffectiveTo.HasValue
				? DateOnly.FromDateTime(policy.EffectiveTo.Value.Date)
				: academicEnd,
			DateOnly.FromDateTime(evaluationDate.Date));

		if (eligibleEnd < eligibleStart)
		{
			return 0m;
		}

		if (rule.AccrualPolicyType == LeaveAccrualPolicyType.Yearly)
		{
			return annualEntitlement;
		}

		var totalAcademicMonths = CountInclusiveMonths(MonthStart(academicStart), MonthStart(academicEnd));
		if (totalAcademicMonths <= 0)
		{
			return 0m;
		}

		var eligibleMonths = CountInclusiveMonths(MonthStart(eligibleStart), MonthStart(eligibleEnd));
		if (eligibleMonths <= 0)
		{
			return 0m;
		}

		var accrued = annualEntitlement * (decimal)eligibleMonths / totalAcademicMonths;
		return Math.Round(accrued, 2, MidpointRounding.AwayFromZero);
	}

	private static int CountInclusiveMonths(DateOnly start, DateOnly end) =>
		end < start
			? 0
			: ((end.Year - start.Year) * 12) + end.Month - start.Month + 1;

	private static DateOnly MonthStart(DateOnly value) => new(value.Year, value.Month, 1);

	private static DateOnly Max(params DateOnly[] values)
	{
		var result = values[0];
		foreach (var value in values[1..])
		{
			if (value > result)
			{
				result = value;
			}
		}

		return result;
	}

	private static DateOnly Min(params DateOnly[] values)
	{
		var result = values[0];
		foreach (var value in values[1..])
		{
			if (value < result)
			{
				result = value;
			}
		}

		return result;
	}
}
