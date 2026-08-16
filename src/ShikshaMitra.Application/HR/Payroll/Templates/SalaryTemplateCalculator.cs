using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.Payroll.Settings;
using Volo.Abp;

namespace ShikshaMitra.HR.Payroll.Templates;

internal sealed record SalaryTemplateComponent(Guid Id, string Name);

internal sealed record SalaryTemplateEvaluation(
	decimal Gross,
	decimal Basic,
	decimal TotalDeductions,
	decimal Net,
	decimal EmployerContributionTotal,
	List<CalculatedSalaryLineDto> Earnings,
	List<CalculatedSalaryLineDto> Deductions,
	List<CalculatedSalaryLineDto> EmployerContributions,
	List<string> Trace);

internal static class SalaryTemplateCalculator
{
	public static void ValidateRules(IReadOnlyList<SalaryTemplateRule> rules)
	{
		ValidateDraftRules(rules);

		if (rules.Count == 0)
		{
			throw new BusinessException("HR:Payroll:SalaryTemplateRulesRequired");
		}

		if (rules.Count(x => x.LineKind == SalaryTemplateLineKind.Earning && x.SemanticType == PayrollComponentSemanticType.Basic) != 1)
		{
			throw new BusinessException("HR:Payroll:SalaryTemplateRequiresOneBasic");
		}

		if (rules.Count(x => x.LineKind == SalaryTemplateLineKind.Earning &&
		                     x.FormulaType == SalaryTemplateFormulaType.RemainderOfGross) != 1 ||
		    rules.Count(x => x.LineKind == SalaryTemplateLineKind.Earning &&
		                     x.FormulaType == SalaryTemplateFormulaType.RemainderOfGross &&
		                     x.SemanticType == PayrollComponentSemanticType.BalancingEarning) != 1)
		{
			throw new BusinessException("HR:Payroll:SalaryTemplateRequiresOneRemainder");
		}

		var basicRule = rules.Single(x =>
			x.LineKind == SalaryTemplateLineKind.Earning &&
			x.SemanticType == PayrollComponentSemanticType.Basic);
		if (rules.Any(x =>
			x.FormulaType == SalaryTemplateFormulaType.PercentageOfBasic &&
			basicRule.DisplayOrder >= x.DisplayOrder))
		{
			throw new BusinessException("HR:Payroll:SalaryTemplateBasicMustPrecedeDependent");
		}
	}

	public static void ValidateDraftRules(IReadOnlyList<SalaryTemplateRule> rules)
	{
		if (rules.Count == 0)
		{
			return;
		}

		var normalizedCodes = rules.Select(x => x.Code.Trim().ToUpperInvariant()).ToList();
		if (normalizedCodes.Distinct().Count() != rules.Count)
		{
			throw new BusinessException("HR:Payroll:SalaryTemplateDuplicateCode");
		}
		if (rules.Select(x => x.ComponentId).Distinct().Count() != rules.Count)
		{
			throw new BusinessException("HR:Payroll:SalaryTemplateDuplicateComponent");
		}
		if (rules.Select(x => x.DisplayOrder).Distinct().Count() != rules.Count)
		{
			throw new BusinessException("HR:Payroll:SalaryTemplateDuplicateDisplayOrder");
		}

		if (rules.Count(x => x.LineKind == SalaryTemplateLineKind.Earning && x.SemanticType == PayrollComponentSemanticType.Basic) > 1)
		{
			throw new BusinessException("HR:Payroll:SalaryTemplateRequiresOneBasic");
		}

		if (rules.Count(x => x.LineKind == SalaryTemplateLineKind.Earning &&
		                     x.FormulaType == SalaryTemplateFormulaType.RemainderOfGross) > 1 ||
		    rules.Count(x => x.SemanticType == PayrollComponentSemanticType.BalancingEarning) > 1)
		{
			throw new BusinessException("HR:Payroll:SalaryTemplateRequiresOneRemainder");
		}

		if (rules.Count(x => x.FormulaType == SalaryTemplateFormulaType.EmployeeProvidentFund) > 1 ||
		    rules.Count(x => x.FormulaType == SalaryTemplateFormulaType.ProfessionalTax) > 1)
		{
			throw new BusinessException("HR:Payroll:SalaryTemplateDuplicateStatutoryLine");
		}

		var codes = normalizedCodes.ToHashSet();
		var rulesByCode = rules.ToDictionary(x => x.Code.Trim(), StringComparer.OrdinalIgnoreCase);
		var basicRule = rules.SingleOrDefault(x => x.LineKind == SalaryTemplateLineKind.Earning && x.SemanticType == PayrollComponentSemanticType.Basic);
		foreach (var rule in rules)
		{
			if (rule.ComponentId == Guid.Empty || rule.DisplayOrder < 0)
			{
				throw new BusinessException("HR:Payroll:SalaryTemplateInvalidRule").WithData("code", rule.Code);
			}

			EnsureCompatibleRule(rule);

			if (rule.FormulaType is SalaryTemplateFormulaType.PercentageOfGross or
			    SalaryTemplateFormulaType.PercentageOfBasic or SalaryTemplateFormulaType.PercentageOfComponent)
			{
				if (rule.Value is null or < 0m or > 100m)
				{
					throw new BusinessException("HR:Payroll:SalaryTemplateInvalidPercentage").WithData("code", rule.Code);
				}
			}
			else if (rule.FormulaType == SalaryTemplateFormulaType.FixedAmount && rule.Value is null or < 0m)
			{
				throw new BusinessException("HR:Payroll:SalaryTemplateInvalidAmount").WithData("code", rule.Code);
			}

			if (rule.FormulaType == SalaryTemplateFormulaType.PercentageOfComponent &&
			    (string.IsNullOrWhiteSpace(rule.BasisCode) || !codes.Contains(rule.BasisCode.Trim().ToUpperInvariant())))
			{
				throw new BusinessException("HR:Payroll:SalaryTemplateInvalidBasis").WithData("code", rule.Code);
			}

			if (rule.FormulaType == SalaryTemplateFormulaType.PercentageOfComponent &&
			    rulesByCode[rule.BasisCode!.Trim()].LineKind != SalaryTemplateLineKind.Earning)
			{
				throw new BusinessException("HR:Payroll:SalaryTemplateInvalidBasis").WithData("code", rule.Code);
			}

			if (rule.FormulaType == SalaryTemplateFormulaType.PercentageOfComponent &&
			    rulesByCode[rule.BasisCode!.Trim()].DisplayOrder >= rule.DisplayOrder)
			{
				throw new BusinessException("HR:Payroll:SalaryTemplateBasisMustPrecedeDependent")
					.WithData("code", rule.Code)
					.WithData("basisCode", rule.BasisCode);
			}

			if (rule.FormulaType == SalaryTemplateFormulaType.PercentageOfBasic &&
			    basicRule != null &&
			    basicRule.DisplayOrder >= rule.DisplayOrder)
			{
				throw new BusinessException("HR:Payroll:SalaryTemplateBasicMustPrecedeDependent")
					.WithData("code", rule.Code);
			}
		}

		EnsureAcyclic(rules);
	}

	private static void EnsureCompatibleRule(SalaryTemplateRule rule)
	{
		var statutoryFormula = rule.FormulaType is SalaryTemplateFormulaType.EmployeeProvidentFund or
			SalaryTemplateFormulaType.ProfessionalTax;
		if (rule.SemanticType == PayrollComponentSemanticType.Basic &&
		    rule.FormulaType is not (SalaryTemplateFormulaType.PercentageOfGross or SalaryTemplateFormulaType.FixedAmount))
		{
			throw new BusinessException("HR:Payroll:SalaryTemplateSemanticFormulaMismatch").WithData("code", rule.Code);
		}
		if (rule.LineKind == SalaryTemplateLineKind.Earning && statutoryFormula)
		{
			throw new BusinessException("HR:Payroll:SalaryTemplateFormulaLineKindMismatch").WithData("code", rule.Code);
		}
		if (rule.LineKind == SalaryTemplateLineKind.Deduction && rule.FormulaType == SalaryTemplateFormulaType.RemainderOfGross)
		{
			throw new BusinessException("HR:Payroll:SalaryTemplateFormulaLineKindMismatch").WithData("code", rule.Code);
		}

		var semanticMatchesFormula = rule.SemanticType switch
		{
			PayrollComponentSemanticType.EmployeeProvidentFund =>
				rule.LineKind == SalaryTemplateLineKind.Deduction && rule.FormulaType == SalaryTemplateFormulaType.EmployeeProvidentFund,
			PayrollComponentSemanticType.ProfessionalTax =>
				rule.LineKind == SalaryTemplateLineKind.Deduction && rule.FormulaType == SalaryTemplateFormulaType.ProfessionalTax,
			PayrollComponentSemanticType.BalancingEarning =>
				rule.LineKind == SalaryTemplateLineKind.Earning && rule.FormulaType == SalaryTemplateFormulaType.RemainderOfGross,
			PayrollComponentSemanticType.Basic or PayrollComponentSemanticType.DearnessAllowance =>
				rule.LineKind == SalaryTemplateLineKind.Earning && !statutoryFormula && rule.FormulaType != SalaryTemplateFormulaType.RemainderOfGross,
			_ => !statutoryFormula && rule.FormulaType != SalaryTemplateFormulaType.RemainderOfGross
		};
		if (!semanticMatchesFormula)
		{
			throw new BusinessException("HR:Payroll:SalaryTemplateSemanticFormulaMismatch").WithData("code", rule.Code);
		}
	}

	public static SalaryTemplateEvaluation Calculate(
		SalaryInputBasis inputBasis,
		decimal monthlyAmount,
		IReadOnlyList<SalaryTemplateRule> rules,
		IReadOnlyDictionary<Guid, SalaryTemplateComponent> components,
		ProvidentFund? pfRule,
		IReadOnlyList<ProfessionalTax> professionalTaxSlabs)
	{
		ValidateRules(rules);
		var gross = inputBasis == SalaryInputBasis.Gross
			? Round(monthlyAmount)
			: SolveGross(monthlyAmount, rules, components, pfRule, professionalTaxSlabs);
		return EvaluateGross(gross, rules, components, pfRule, professionalTaxSlabs);
	}

	private static decimal SolveGross(
		decimal targetNet,
		IReadOnlyList<SalaryTemplateRule> rules,
		IReadOnlyDictionary<Guid, SalaryTemplateComponent> components,
		ProvidentFund? pfRule,
		IReadOnlyList<ProfessionalTax> slabs)
	{
		var taxAmounts = slabs.Select(x => x.TaxAmount).Append(0m).Distinct().OrderBy(x => x).ToList();
		var candidates = new List<decimal>();
		foreach (var forcedTax in taxAmounts)
		{
			var gross = Math.Max(0.01m, targetNet + forcedTax);
			for (var iteration = 0; iteration < 100; iteration++)
			{
				var evaluation = EvaluateGross(gross, rules, components, pfRule, slabs, forcedTax);
				var next = Round(targetNet + evaluation.TotalDeductions);
				if (Math.Abs(next - gross) <= 0.01m)
				{
					gross = next;
					break;
				}

				gross = next;
			}

			var actual = EvaluateGross(gross, rules, components, pfRule, slabs);
			if (Math.Abs(actual.Net - targetNet) <= 0.01m)
			{
				candidates.Add(gross);
			}
		}

		if (candidates.Count == 0)
		{
			throw new BusinessException("HR:Payroll:TakeHomeTargetNotSolvable")
				.WithData("targetNet", targetNet);
		}

		return candidates.Min();
	}

	private static SalaryTemplateEvaluation EvaluateGross(
		decimal gross,
		IReadOnlyList<SalaryTemplateRule> rules,
		IReadOnlyDictionary<Guid, SalaryTemplateComponent> components,
		ProvidentFund? pfRule,
		IReadOnlyList<ProfessionalTax> professionalTaxSlabs,
		decimal? forcedProfessionalTax = null)
	{
		var values = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
		var earnings = new List<CalculatedSalaryLineDto>();
		var deductions = new List<CalculatedSalaryLineDto>();
		var employerContributions = new List<CalculatedSalaryLineDto>();
		var trace = new List<string>();
		var orderedRules = rules.OrderBy(x => x.DisplayOrder).ToList();
		var basicRule = orderedRules.Single(x => x.LineKind == SalaryTemplateLineKind.Earning && x.SemanticType == PayrollComponentSemanticType.Basic);

		foreach (var rule in orderedRules.Where(x => x.LineKind == SalaryTemplateLineKind.Earning && x.FormulaType != SalaryTemplateFormulaType.RemainderOfGross))
		{
			var amount = CalculateOrdinaryAmount(rule, gross, values.GetValueOrDefault(basicRule.Code), values);
			values[rule.Code] = amount;
			earnings.Add(ToLine(rule, components, amount));
			trace.Add($"{GetName(rule, components)} = {Describe(rule, amount)}");
		}

		var remainderRule = orderedRules.Single(x => x.LineKind == SalaryTemplateLineKind.Earning && x.FormulaType == SalaryTemplateFormulaType.RemainderOfGross);
		var remainder = Round(gross - earnings.Sum(x => x.Amount));
		if (remainder < 0m)
		{
			throw new BusinessException("HR:Payroll:SalaryTemplateEarningsExceedGross").WithData("gross", gross);
		}

		values[remainderRule.Code] = remainder;
		earnings.Add(ToLine(remainderRule, components, remainder));
		trace.Add($"{GetName(remainderRule, components)} = gross remainder = {remainder:0.00}");
		var basic = values[basicRule.Code];

		foreach (var rule in orderedRules.Where(x => x.LineKind == SalaryTemplateLineKind.Deduction))
		{
			decimal amount;
			Guid? statutoryRuleId = null;
			switch (rule.FormulaType)
			{
				case SalaryTemplateFormulaType.EmployeeProvidentFund:
					if (pfRule == null)
					{
						continue;
					}

					var eligibleWage = basic;
					if (pfRule.ContributionBasis == PfContributionBasis.BasicAndDearnessAllowance)
					{
						eligibleWage += orderedRules
							.Where(x => x.LineKind == SalaryTemplateLineKind.Earning &&
							            x.SemanticType == PayrollComponentSemanticType.DearnessAllowance)
							.Sum(x => values.GetValueOrDefault(x.Code));
					}
					if (pfRule.WageCeiling.HasValue)
					{
						eligibleWage = Math.Min(eligibleWage, pfRule.WageCeiling.Value);
					}
					amount = Round(eligibleWage * pfRule.EmployeePercentage / 100m);
					var employerAmount = Round(eligibleWage * pfRule.EmployerPercentage / 100m);
					statutoryRuleId = pfRule.Id;
					employerContributions.Add(new CalculatedSalaryLineDto
					{
						ComponentId = rule.ComponentId,
						Code = "EMPLOYER_PF",
						Name = "Employer Provident Fund",
						Amount = employerAmount,
						SemanticType = ToContractEnum(PayrollComponentSemanticType.EmployeeProvidentFund.ToString()),
						FormulaType = ToContractEnum(rule.FormulaType.ToString()),
						IsStatutory = true
					});
					trace.Add($"Employee PF = {eligibleWage:0.00} x {pfRule.EmployeePercentage:0.####}% = {amount:0.00}");
					break;
				case SalaryTemplateFormulaType.ProfessionalTax:
					amount = forcedProfessionalTax ?? ResolveProfessionalTax(gross, professionalTaxSlabs);
					trace.Add($"Professional Tax for gross {gross:0.00} = {amount:0.00}");
					break;
				default:
					amount = CalculateOrdinaryAmount(rule, gross, basic, values);
					trace.Add($"{GetName(rule, components)} = {Describe(rule, amount)}");
					break;
			}

			var line = ToLine(rule, components, amount);
			line.IsStatutory = rule.FormulaType is SalaryTemplateFormulaType.EmployeeProvidentFund or SalaryTemplateFormulaType.ProfessionalTax;
			deductions.Add(line);
			values[rule.Code] = amount;
		}

		var totalDeductions = Round(deductions.Sum(x => x.Amount));
		return new SalaryTemplateEvaluation(
			gross,
			basic,
			totalDeductions,
			Round(gross - totalDeductions),
			Round(employerContributions.Sum(x => x.Amount)),
			earnings.OrderBy(x => rules.Single(r => r.Code == x.Code).DisplayOrder).ToList(),
			deductions.OrderBy(x => rules.Single(r => r.Code == x.Code).DisplayOrder).ToList(),
			employerContributions,
			trace);
	}

	private static decimal CalculateOrdinaryAmount(
		SalaryTemplateRule rule,
		decimal gross,
		decimal basic,
		IReadOnlyDictionary<string, decimal> values) => rule.FormulaType switch
	{
		SalaryTemplateFormulaType.PercentageOfGross => Round(gross * (rule.Value ?? 0m) / 100m),
		SalaryTemplateFormulaType.PercentageOfBasic => Round(basic * (rule.Value ?? 0m) / 100m),
		SalaryTemplateFormulaType.PercentageOfComponent => Round(values.GetValueOrDefault(rule.BasisCode ?? string.Empty) * (rule.Value ?? 0m) / 100m),
		SalaryTemplateFormulaType.FixedAmount => Round(rule.Value ?? 0m),
		_ => 0m
	};

	private static decimal ResolveProfessionalTax(decimal gross, IReadOnlyList<ProfessionalTax> slabs) =>
		slabs
			.Where(x => gross >= x.FromSalary && (!x.ToSalary.HasValue || gross <= x.ToSalary.Value))
			.OrderByDescending(x => x.FromSalary)
			.Select(x => x.TaxAmount)
			.FirstOrDefault();

	private static CalculatedSalaryLineDto ToLine(
		SalaryTemplateRule rule,
		IReadOnlyDictionary<Guid, SalaryTemplateComponent> components,
		decimal amount) => new()
	{
		ComponentId = rule.ComponentId,
		Code = rule.Code,
		Name = GetName(rule, components),
		Amount = amount,
		SemanticType = ToContractEnum(rule.SemanticType.ToString()),
		FormulaType = ToContractEnum(rule.FormulaType.ToString()),
		IsStatutory = false
	};

	private static string GetName(SalaryTemplateRule rule, IReadOnlyDictionary<Guid, SalaryTemplateComponent> components) =>
		components.TryGetValue(rule.ComponentId, out var component) ? component.Name : rule.Code;

	private static string Describe(SalaryTemplateRule rule, decimal amount) => rule.FormulaType switch
	{
		SalaryTemplateFormulaType.PercentageOfGross => $"gross x {rule.Value:0.####}% = {amount:0.00}",
		SalaryTemplateFormulaType.PercentageOfBasic => $"basic x {rule.Value:0.####}% = {amount:0.00}",
		SalaryTemplateFormulaType.PercentageOfComponent => $"{rule.BasisCode} x {rule.Value:0.####}% = {amount:0.00}",
		SalaryTemplateFormulaType.FixedAmount => $"fixed {amount:0.00}",
		_ => amount.ToString("0.00")
	};

	private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

	private static string ToContractEnum(string value) =>
		string.Concat(value.Select((character, index) => index > 0 && char.IsUpper(character) ? "_" + character : character.ToString()))
			.ToUpperInvariant();

	private static void EnsureAcyclic(IReadOnlyList<SalaryTemplateRule> rules)
	{
		var dependencies = rules
			.Where(x => x.FormulaType == SalaryTemplateFormulaType.PercentageOfComponent)
			.ToDictionary(x => x.Code, x => x.BasisCode!, StringComparer.OrdinalIgnoreCase);
		foreach (var code in dependencies.Keys)
		{
			var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var current = code;
			while (dependencies.TryGetValue(current, out var next))
			{
				if (!visited.Add(current))
				{
					throw new BusinessException("HR:Payroll:SalaryTemplateDependencyCycle").WithData("code", code);
				}

				current = next;
			}
		}
	}
}
