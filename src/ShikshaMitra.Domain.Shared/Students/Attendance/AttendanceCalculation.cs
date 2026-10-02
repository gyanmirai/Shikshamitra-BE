using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.Students.Attendance;

public sealed record AttendanceCalculationDay(DateTime Date, bool Expected, bool Recorded, double PresentCredit,
	double DenominatorCredit = 1, bool IsProvisional = false, AttendanceStatus Status = AttendanceStatus.Pending);
public sealed record AttendanceCalculationResult(int ExpectedDays, int RecordedDays, int MissingDays, int PresentDays,
	double PresentCredit, double DenominatorDays, double? Percentage, bool IsProvisional);

/// <summary>One date is one attendance outcome, even when dated placements span multiple divisions.</summary>
public static class AttendanceCalculation
{
	public static AttendanceCalculationResult Calculate(IEnumerable<AttendanceCalculationDay> source)
	{
		var rows = source.GroupBy(d => d.Date.Date).Select(g => g.Count(d => d.Recorded) > 1
			? new AttendanceCalculationDay(g.Key, g.Any(d => d.Expected), false, 0, IsProvisional: true)
			: g.FirstOrDefault(d => d.Recorded) ?? g.FirstOrDefault(d => d.Expected) ?? g.First()).ToList();
		var expected = rows.Where(d => d.Expected).ToList();
		var recorded = expected.Where(d => d.Recorded).ToList();
		var presentCredit = recorded.Sum(d => d.PresentCredit);
		var denominator = recorded.Sum(d => d.DenominatorCredit);
		return new(expected.Count, recorded.Count, expected.Count - recorded.Count, recorded.Count(d => d.Status == AttendanceStatus.Present),
			presentCredit, denominator, denominator == 0 ? null : Math.Round(presentCredit * 100d / denominator, 2),
			rows.Any(d => d.IsProvisional) || expected.Count > recorded.Count);
	}
}
