using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.Payroll.Services;

namespace ShikshaMitra.HR.Payroll.Templates;

internal static class SalaryTemplateAllocation
{
	public static (decimal Minimum, decimal Maximum) GetGrossBounds(IReadOnlyList<SalaryTemplateRule> rules, decimal maximum) =>
		CompensationTargetSolver.GrossBounds(rules.Where(x => x.LineKind == SalaryTemplateLineKind.Earning)
			.Select(x => new GrossEarningRule(x.ComponentId, x.SemanticType == PayrollComponentSemanticType.Basic,
				x.FormulaType, x.Value, x.BasisCode == null ? null : rules.Single(basis => basis.Code.Equals(x.BasisCode, StringComparison.OrdinalIgnoreCase)).ComponentId)).ToList(), maximum);
}
