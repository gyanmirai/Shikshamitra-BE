using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.Payroll.Templates;
using Volo.Abp;

namespace ShikshaMitra.HR.Payroll.Services;

/// <summary>Searches whole paise within each statutory interval, then verifies a strict allocation.</summary>
public static class CompensationTargetSolver
{
	public const string NotSolvable = "HR:Payroll:CompensationTargetNotSolvable";

	public static (decimal Minimum, decimal Maximum) GrossBounds(
		IReadOnlyList<GrossEarningRule> rules, decimal maximum, decimal factor = 1m)
	{
		if (rules.Any(x => x.FormulaType == SalaryTemplateFormulaType.RemainderOfGross)) return (0m, maximum);
		var values = new Dictionary<Guid, (decimal Slope, decimal Fixed)>();
		var visiting = new HashSet<Guid>();
		(decimal Slope, decimal Fixed) Evaluate(Guid id)
		{
			if (values.TryGetValue(id, out var value)) return value;
			if (!visiting.Add(id)) throw new BusinessException(HRErrorCodes.PayrollComponentDependencyCycle);
			var rule = rules.SingleOrDefault(x => x.ComponentId == id)
				?? throw new BusinessException(HRErrorCodes.PayrollComponentDependencyTargetInvalid);
			var rate = rule.Value.GetValueOrDefault() / 100m;
			var basis = rule.FormulaType switch
			{
				SalaryTemplateFormulaType.PercentageOfBasic => Evaluate(rules.FirstOrDefault(x => x.IsBasic)?.ComponentId ?? Guid.Empty),
				SalaryTemplateFormulaType.PercentageOfComponent => Evaluate(rule.BasisComponentId ?? Guid.Empty),
				_ => (Slope: 0m, Fixed: 0m)
			};
			value = rule.FormulaType switch
			{
				SalaryTemplateFormulaType.FixedAmount => (0m, GrossEarningAllocation.Money(rule.Value.GetValueOrDefault()) * factor),
				SalaryTemplateFormulaType.PercentageOfGross => (rate, 0m),
				_ => (basis.Slope * rate, basis.Fixed * rate)
			};
			values[id] = value;
			visiting.Remove(id);
			return value;
		}
		foreach (var rule in rules) Evaluate(rule.ComponentId);
		var divisor = 1m - values.Values.Sum(x => x.Slope);
		var fixedTotal = values.Values.Sum(x => x.Fixed);
		if (divisor == 0m) return fixedTotal == 0m ? (0m, maximum) : (1m, 0m);
		var onlyGross = fixedTotal / divisor;
		return (Math.Max(0m, Math.Ceiling(onlyGross * 100m) / 100m),
			Math.Min(maximum, Math.Floor(onlyGross * 100m) / 100m));
	}

	public static decimal Solve(decimal target, bool protectTakeHome, decimal minimum, decimal maximum,
		IEnumerable<decimal> statutoryBoundaries, Func<decimal, bool, decimal?> evaluate, int roundingWindow = 200)
	{
		var boundaries = statutoryBoundaries.Append(minimum).Append(maximum)
			.Select(GrossEarningAllocation.Money).Where(x => x >= minimum && x <= maximum)
			.Distinct().OrderBy(x => x).ToList();
		var candidates = new Dictionary<decimal, decimal>();
		void Add(decimal gross)
		{
			if (gross < minimum || gross > maximum || candidates.ContainsKey(gross)) return;
			var achieved = evaluate(gross, false);
			if (achieved.HasValue && (protectTakeHome ? achieved >= target : achieved <= target))
				candidates[gross] = achieved.Value;
		}
		foreach (var boundary in boundaries) Add(boundary);
		for (var index = 0; index < boundaries.Count - 1; index++)
		{
			var first = decimal.ToInt64(boundaries[index] * 100m);
			var last = decimal.ToInt64(boundaries[index + 1] * 100m) - 1;
			var low = first;
			var high = last;
			while (low <= high)
			{
				var mid = low + (high - low) / 2;
				var achieved = evaluate(mid / 100m, true);
				if (!achieved.HasValue) { low = mid + 1; continue; }
				Add(mid / 100m);
				if (protectTakeHome ? achieved >= target : achieved > target) high = mid - 1;
				else low = mid + 1;
			}
			// ESI rupee rounding introduces small downward steps in take-home.
			for (var paise = Math.Max(first, high - roundingWindow); paise <= Math.Min(last, low + roundingWindow); paise++)
				Add(paise / 100m);
		}
		if (candidates.Count == 0) throw new BusinessException(NotSolvable).WithData("target", target);
		var selected = protectTakeHome
			? candidates.OrderBy(x => x.Key).First()
			: candidates.OrderByDescending(x => x.Key).First();
		if (Math.Abs(selected.Value - target) > 0.01m)
			throw new BusinessException(NotSolvable).WithData("target", target).WithData("achieved", selected.Value);
		return selected.Key;
	}
}
