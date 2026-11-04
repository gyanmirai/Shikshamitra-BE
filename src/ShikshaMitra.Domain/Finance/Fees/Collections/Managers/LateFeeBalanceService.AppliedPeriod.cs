using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ShikshaMitra.Finance.Fees.FeeCollection;

namespace ShikshaMitra.Finance.Fees.Collections;

public partial class LateFeeBalanceService
{
	/// <summary>Actually counted calendar days, with inclusive endpoints.</summary>
	public sealed record AppliedInterval(DateOnly FromDate, DateOnly ThroughDate);

	private static IReadOnlyList<AppliedInterval> AppliedPeriod(
		IEnumerable<(DateTime Start, DateTime End)> source, int countedDays)
	{
		// The calculator's internal boundary is exclusive: a due date plus grace
		// contributes its first charged calendar day on the following date.
		var intervals = MergeAppliedIntervals(source.Where(x => x.End.Date > x.Start.Date)
			.Select(x => new AppliedInterval(DateOnly.FromDateTime(x.Start.Date.AddDays(1)), DateOnly.FromDateTime(x.End.Date))));
		var result = new List<AppliedInterval>();
		var remaining = countedDays;
		foreach (var interval in intervals)
		{
			if (remaining <= 0) break;
			var days = Math.Min(remaining, interval.ThroughDate.DayNumber - interval.FromDate.DayNumber + 1);
			result.Add(new(interval.FromDate, interval.FromDate.AddDays(days - 1)));
			remaining -= days;
		}
		return result;
	}

	private static List<AppliedInterval> MergeAppliedIntervals(IEnumerable<AppliedInterval> source)
	{
		var result = new List<AppliedInterval>();
		foreach (var interval in source.OrderBy(x => x.FromDate).ThenBy(x => x.ThroughDate))
		{
			if (result.Count > 0 && interval.FromDate.DayNumber <= result[^1].ThroughDate.DayNumber + 1)
				result[^1] = result[^1] with { ThroughDate = interval.ThroughDate > result[^1].ThroughDate ? interval.ThroughDate : result[^1].ThroughDate };
			else result.Add(interval);
		}
		return result;
	}

	private static IReadOnlyList<AppliedInterval>? ReadAssessmentIntervals(DueFee due)
	{
		var saved = due.GetLateFeeAssessmentPeriodJson();
		if (string.IsNullOrWhiteSpace(saved)) return null;
		try
		{
			var intervals = JsonSerializer.Deserialize<List<AppliedInterval>>(saved);
			return intervals is { Count: > 0 } && intervals.All(x => x != null && x.ThroughDate >= x.FromDate)
				? MergeAppliedIntervals(intervals) : null;
		}
		catch (JsonException) { return null; }
	}
}
