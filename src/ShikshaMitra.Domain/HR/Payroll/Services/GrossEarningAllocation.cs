using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.Payroll.Templates;
using Volo.Abp;

namespace ShikshaMitra.HR.Payroll.Services;

public sealed record GrossEarningRule(Guid ComponentId, bool IsBasic,
	SalaryTemplateFormulaType FormulaType, decimal? Value, Guid? BasisComponentId = null);

/// <summary>Evaluates earning dependencies before assigning whole paise, preserving regular gross.</summary>
public static class GrossEarningAllocation
{
	// Decimal arithmetic can leave a residue below financial precision in long dependency chains.
	private const decimal ArithmeticTolerance = 0.000000000000000001m;
	public static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

	public static IReadOnlyDictionary<Guid, decimal> Calculate(decimal gross,
		IReadOnlyList<GrossEarningRule> rules, decimal fixedAmountFactor = 1m, bool allowUnallocatedProbe = false)
	{
		if (rules.Select(x => x.ComponentId).Distinct().Count() != rules.Count ||
			rules.Count(x => x.FormulaType == SalaryTemplateFormulaType.RemainderOfGross) > 1)
			throw new BusinessException(HRErrorCodes.SalaryTemplateAllocationMismatch);
		var byId = rules.ToDictionary(x => x.ComponentId);
		foreach (var rule in rules)
		{
			if (rule.FormulaType is SalaryTemplateFormulaType.PercentageOfGross or SalaryTemplateFormulaType.PercentageOfBasic or SalaryTemplateFormulaType.PercentageOfComponent && rule.Value is null or < 0m or > 100m)
				throw new BusinessException(HRErrorCodes.PayrollComponentInvalidPercentage);
			if (rule.FormulaType == SalaryTemplateFormulaType.FixedAmount && rule.Value is null or < 0m)
				throw new BusinessException("HR:Payroll:SalaryTemplateInvalidAmount");
		}
		var values = new Dictionary<Guid, decimal>();
		var visiting = new HashSet<Guid>();
		decimal Evaluate(Guid id)
		{
			if (values.TryGetValue(id, out var cached)) return cached;
			if (!byId.TryGetValue(id, out var rule))
				throw new BusinessException(HRErrorCodes.PayrollComponentDependencyTargetInvalid);
			if (!visiting.Add(id)) throw new BusinessException(HRErrorCodes.PayrollComponentDependencyCycle);
			var rate = rule.Value.GetValueOrDefault() / 100m;
			var amount = rule.FormulaType switch
			{
				SalaryTemplateFormulaType.FixedAmount => Money(rule.Value.GetValueOrDefault()) * fixedAmountFactor,
				SalaryTemplateFormulaType.PercentageOfGross => gross * rate,
				SalaryTemplateFormulaType.PercentageOfBasic => Evaluate(rules.FirstOrDefault(x => x.IsBasic)?.ComponentId ?? Guid.Empty) * rate,
				SalaryTemplateFormulaType.PercentageOfComponent => Evaluate(rule.BasisComponentId ?? Guid.Empty) * rate,
				SalaryTemplateFormulaType.RemainderOfGross => gross - rules.Where(x => x.ComponentId != id).Sum(x => Evaluate(x.ComponentId)),
				_ => throw new BusinessException("HR:Payroll:SalaryTemplateSemanticFormulaMismatch")
			};
			if (amount < -ArithmeticTolerance)
				throw AllocationError(gross, gross - amount, "HR:Payroll:SalaryTemplateEarningsExceedGross")
					.WithData("excess", -amount);
			values[id] = Math.Max(0m, amount);
			visiting.Remove(id);
			return values[id];
		}
		foreach (var rule in rules) Evaluate(rule.ComponentId);
		var allocated = values.Values.Sum();
		if (Math.Abs(gross - allocated) > ArithmeticTolerance)
		{
			// Solver probes explore the target function; only a strict allocation can become a result.
			if (allowUnallocatedProbe) return values.ToDictionary(x => x.Key, x => Money(x.Value));
			throw AllocationError(gross, allocated, HRErrorCodes.SalaryTemplateAllocationMismatch);
		}
		var result = values.ToDictionary(x => x.Key, x => Math.Floor(x.Value * 100m) / 100m);
		var paise = decimal.ToInt32((Money(gross) - result.Values.Sum()) * 100m);
		foreach (var rule in rules.OrderByDescending(x => values[x.ComponentId] - result[x.ComponentId])
			.ThenBy(x => x.IsBasic).ThenBy(x => x.ComponentId).Take(paise))
			result[rule.ComponentId] += 0.01m;
		return result;
	}

	private static BusinessException AllocationError(decimal gross, decimal allocated, string code) =>
		new BusinessException(code)
			.WithData("gross", gross).WithData("allocated", allocated).WithData("difference", gross - allocated)
			.WithData("message", FormattableString.Invariant($"Gross is INR {gross:0.00##########################}; regular earnings allocate INR {allocated:0.00##########################}; {(gross >= allocated ? "missing" : "excess")} INR {Math.Abs(gross - allocated):0.00##########################}. Adjust the earning rules or choose an explicit remainder component."));
}
