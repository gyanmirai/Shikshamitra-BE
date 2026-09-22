using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Templates;
using Volo.Abp;

namespace ShikshaMitra.HR.Payroll.Services;

public sealed record PayrollComponentFormula(
	SalaryTemplateFormulaType Type,
	decimal? Value,
	Guid? BasisComponentId = null);

/// <summary>Resolves ordinary payroll formulas against earned regular gross, once.</summary>
internal sealed class PayrollOrdinaryComponentCalculator(
	IReadOnlyList<EmployeePayrollComponent> components,
	decimal gross,
	decimal factor)
{
	private readonly Dictionary<Guid, decimal> _amounts = new();
	private readonly HashSet<Guid> _visiting = [];

	public decimal Calculate(EmployeePayrollComponent component)
	{
		if (_amounts.TryGetValue(component.Id, out var existing)) return existing;
		if (!_visiting.Add(component.Id))
			throw new BusinessException(HRErrorCodes.PayrollComponentDependencyCycle);

		var formula = component.Formula;
		var amount = component.IsTemp || formula == null
			? PayrollProrationCalculator.ProrateAmount(component.Amount, component.IsTemp, factor)
			: formula.Type switch
			{
				SalaryTemplateFormulaType.FixedAmount => GrossEarningAllocation.Money(formula.Value ?? component.Amount) * factor,
				SalaryTemplateFormulaType.PercentageOfGross => gross * RequiredValue(formula) / 100m,
				SalaryTemplateFormulaType.PercentageOfBasic => Calculate(RequireBasis(components.FirstOrDefault(x =>
					x.IsEarning && !x.IsTemp && x.SemanticType == PayrollComponentSemanticType.Basic))) * RequiredValue(formula) / 100m,
				SalaryTemplateFormulaType.PercentageOfComponent => Calculate(RequireBasis(components.FirstOrDefault(x =>
					x.IsEarning && !x.IsTemp && x.ComponentId == formula.BasisComponentId))) * RequiredValue(formula) / 100m,
				SalaryTemplateFormulaType.RemainderOfGross when component.IsEarning => gross - components
					.Where(x => x.IsEarning && !x.IsTemp && x.Id != component.Id).Sum(Calculate),
				_ => throw new BusinessException("HR:Payroll:SalaryTemplateSemanticFormulaMismatch")
			};
		if (amount < 0m)
			throw new BusinessException("HR:Payroll:SalaryTemplateEarningsExceedGross");
		var rounded = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
		_amounts[component.Id] = rounded;
		_visiting.Remove(component.Id);
		return rounded;
	}

	public List<PayrollComputedLineItem> CalculateEarnings()
	{
		var regular = components.Where(x => x.IsEarning && !x.IsTemp).ToList();
		var rawGross = regular.Sum(x => x.Amount) * factor;
		var finalized = GrossEarningAllocation.Calculate(rawGross, regular.Select(x => new GrossEarningRule(
			x.ComponentId ?? x.Id, x.SemanticType == PayrollComponentSemanticType.Basic,
			x.Formula?.Type ?? SalaryTemplateFormulaType.FixedAmount,
			x.Formula?.Value ?? x.Amount, x.Formula?.BasisComponentId)).ToList(), factor);
		foreach (var component in regular) _amounts[component.Id] = finalized[component.ComponentId ?? component.Id];
		return components.Where(x => x.IsEarning).Select(x => new PayrollComputedLineItem(x, Calculate(x))).ToList();
	}

	private static EmployeePayrollComponent RequireBasis(EmployeePayrollComponent? basis) =>
		basis ?? throw new BusinessException(HRErrorCodes.PayrollComponentDependencyTargetInvalid);

	private static decimal RequiredValue(PayrollComponentFormula formula) =>
		formula.Value is >= 0m and <= 100m ? formula.Value.Value
			: throw new BusinessException(HRErrorCodes.PayrollComponentInvalidPercentage);
}
