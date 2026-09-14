using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.HR.Payroll.Templates;

internal static class SalaryTemplateAllocation
{
	// Bound every rounding operation, including chained percentage dependencies.
	// Gross values outside this interval cannot reconcile and must not enter the target solver.
	public static (decimal Minimum, decimal Maximum) GetGrossBounds(IReadOnlyList<SalaryTemplateRule> rules, decimal maximum)
	{
		if (rules.Any(x => x.FormulaType == SalaryTemplateFormulaType.RemainderOfGross)) return (0.01m, maximum);
		var values = new Dictionary<string, (decimal Slope, decimal Fixed, decimal Error)>(StringComparer.OrdinalIgnoreCase);
		var basicCode = rules.Single(x => x.SemanticType == PayrollComponentSemanticType.Basic).Code;
		foreach (var rule in rules.Where(x => x.LineKind == SalaryTemplateLineKind.Earning).OrderBy(x => x.DisplayOrder))
		{
			var rate = (rule.Value ?? 0m) / 100m;
			var basis = rule.FormulaType == SalaryTemplateFormulaType.PercentageOfBasic
				? values.GetValueOrDefault(basicCode) : values.GetValueOrDefault(rule.BasisCode ?? "");
			values[rule.Code] = rule.FormulaType switch
			{
				SalaryTemplateFormulaType.FixedAmount => (0m, Math.Round(rule.Value ?? 0m, 2, MidpointRounding.AwayFromZero), 0m),
				SalaryTemplateFormulaType.PercentageOfGross => (rate, 0m, 0.005m),
				_ => (basis.Slope * rate, basis.Fixed * rate, basis.Error * rate + 0.005m)
			};
		}
		var divisor = 1m - values.Values.Sum(x => x.Slope);
		var fixedTotal = values.Values.Sum(x => x.Fixed);
		var error = values.Values.Sum(x => x.Error);
		if (divisor == 0m) return fixedTotal > error ? (1m, 0m) : (0.01m, maximum);
		var first = (fixedTotal - error) / divisor;
		var last = (fixedTotal + error) / divisor;
		return (Math.Max(0.01m, Math.Ceiling(Math.Min(first, last) * 100m) / 100m),
			Math.Min(maximum, Math.Floor(Math.Max(first, last) * 100m) / 100m));
	}
}
