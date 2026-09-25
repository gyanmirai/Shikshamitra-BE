using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Payroll.Statutory;
using Volo.Abp;

namespace ShikshaMitra.HR.Payroll.Templates;

internal sealed record SalaryTemplateComponent(Guid Id, string Name);

internal sealed record SalaryTemplateEsiEvaluationContext(
	DateTime EffectiveMonth,
	StatutoryRuleVersion Rule,
	StatutoryEstablishment Establishment,
	EmployeeStatutoryEnrollment? Enrollment,
	IReadOnlyDictionary<Guid, StatutoryWageTreatment> ComponentTreatments,
	IReadOnlyList<StatutoryOverride> Overrides,
	Guid DeductionComponentId,
	string DeductionComponentName);

internal sealed record SalaryTemplateEvaluation(
	decimal Gross,
	decimal Basic,
	decimal TotalDeductions,
	decimal Net,
	decimal EmployerContributionTotal,
	List<CalculatedSalaryLineDto> Earnings,
	List<CalculatedSalaryLineDto> Deductions,
	List<CalculatedSalaryLineDto> EmployerContributions,
	List<string> Trace,
	EsiStatutoryCalculationResult? EmployeeStateInsurance)
{
	public decimal StatutoryTakeHome => Gross - Deductions.Where(x => x.IsStatutory).Sum(x => x.Amount);
	public decimal EmployerCost => Math.Round(Gross + EmployerContributionTotal, 2, MidpointRounding.AwayFromZero);
}

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

			// Remainder already depends on every other earning, so this edge closes a cycle.
			if (rule.LineKind == SalaryTemplateLineKind.Earning &&
				rule.FormulaType == SalaryTemplateFormulaType.PercentageOfComponent &&
				rulesByCode[rule.BasisCode!.Trim()].FormulaType == SalaryTemplateFormulaType.RemainderOfGross)
			{
				throw new BusinessException("HR:Payroll:SalaryTemplateDependencyCycle").WithData("code", rule.Code);
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
		IReadOnlyList<ProfessionalTax> professionalTaxSlabs,
		SalaryTemplateEsiEvaluationContext? esiContext = null)
	{
		ValidateRules(rules);
		monthlyAmount = Round(monthlyAmount);
		if (monthlyAmount <= 0m) throw new BusinessException("HR:Payroll:SalaryTemplateInvalidAmount");
		var gross = inputBasis == SalaryInputBasis.Gross
			? Round(monthlyAmount)
			: SolveGross(inputBasis, monthlyAmount, rules, components, pfRule, professionalTaxSlabs, esiContext);
		return EvaluateGross(gross, rules, components, pfRule, professionalTaxSlabs, esiContext: esiContext);
	}

	private static decimal SolveGross(
		SalaryInputBasis inputBasis,
		decimal target,
		IReadOnlyList<SalaryTemplateRule> rules,
		IReadOnlyDictionary<Guid, SalaryTemplateComponent> components,
		ProvidentFund? pfRule,
		IReadOnlyList<ProfessionalTax> slabs,
		SalaryTemplateEsiEvaluationContext? esiContext)
	{
		var upperBound = inputBasis == SalaryInputBasis.EmployerCost
			? Round(target)
			: Round(Math.Min(100_000_000m, Math.Max(target * 2m + 100_000m, target + 1_000_000m)));
		var (allocationMinimum, allocationMaximum) = SalaryTemplateAllocation.GetGrossBounds(rules, upperBound);
		if (allocationMinimum > allocationMaximum)
		{
			throw new BusinessException("HR:Payroll:CompensationTargetNotSolvable")
				.WithData("salaryBasis", inputBasis.ToString()).WithData("target", target);
		}
		var breakpoints = new SortedSet<decimal> { allocationMinimum, allocationMaximum };
		foreach (var slab in slabs)
		{
			AddBreakpoint(breakpoints, slab.FromSalary, upperBound);
			if (slab.ToSalary.HasValue)
			{
				AddBreakpoint(breakpoints, slab.ToSalary.Value + 0.01m, upperBound);
			}
		}

		AddEsiCoverageBreakpoints(
			breakpoints,
			upperBound,
			rules,
			components,
			pfRule,
			slabs,
			esiContext);

		return CompensationTargetSolver.Solve(target, inputBasis == SalaryInputBasis.TakeHome,
			allocationMinimum, allocationMaximum, breakpoints, (gross, probe) =>
			{
				var evaluation = TryEvaluateGross(gross, rules, components, pfRule, slabs, esiContext, probe);
				return evaluation == null ? null : inputBasis == SalaryInputBasis.TakeHome
					? evaluation.StatutoryTakeHome : evaluation.EmployerCost;
			}, esiContext == null ? 2 : 200);
	}

	private static SalaryTemplateEvaluation EvaluateGross(
		decimal gross,
		IReadOnlyList<SalaryTemplateRule> rules,
		IReadOnlyDictionary<Guid, SalaryTemplateComponent> components,
		ProvidentFund? pfRule,
		IReadOnlyList<ProfessionalTax> professionalTaxSlabs,
		SalaryTemplateEsiEvaluationContext? esiContext = null,
		bool allowUnallocated = false)
	{
		var sources = rules.Select(rule => new EmployeePayrollComponent(rule.ComponentId, Guid.Empty,
			GetName(rule, components), rule.ComponentId, null, 0m, true, false, null,
			SalaryComponentType.Fixed, SalaryComponentDependency.None,
			rule.LineKind == SalaryTemplateLineKind.Earning, false, rule.SemanticType)
		{
			Formula = new PayrollComponentFormula(rule.FormulaType, rule.Value,
				rule.BasisCode == null ? null : rules.Single(x => x.Code.Equals(rule.BasisCode, StringComparison.OrdinalIgnoreCase)).ComponentId)
		}).ToList();
		var esiSource = esiContext == null ? null : new EmployeePayrollComponent(
			esiContext.DeductionComponentId, Guid.Empty, esiContext.DeductionComponentName,
			esiContext.DeductionComponentId, null, 0m, true, false, null, SalaryComponentType.Fixed,
			SalaryComponentDependency.None, false, false, PayrollComponentSemanticType.EmployeeStateInsurance);
		var statutory = new PayrollStatutoryRuleSet(pfRule, professionalTaxSlabs,
			EmployeeStateInsurance: esiContext == null ? null : new PayrollEsiStatutoryContext(
				esiContext.EffectiveMonth, esiContext.Rule, esiContext.Establishment, esiContext.Enrollment,
				esiContext.ComponentTreatments, esiContext.Overrides, esiSource!));
		var result = PayrollComputation.Calculate(1, 0, 1m, 1m, sources, statutory, gross, allowUnallocated);
		CalculatedSalaryLineDto Map(SalaryTemplateRule rule)
		{
			var computed = result.Earnings.Concat(result.Deductions).FirstOrDefault(x => x.Source.ComponentId == rule.ComponentId);
			var line = ToLine(rule, components, computed?.Amount ?? 0m);
			line.BasisComponentId = sources.Single(x => x.ComponentId == rule.ComponentId).Formula?.BasisComponentId;
			line.IsStatutory = rule.SemanticType is PayrollComponentSemanticType.EmployeeProvidentFund or PayrollComponentSemanticType.ProfessionalTax;
			if (line.IsStatutory) { line.Persistable = false; line.LineSource = "SYSTEM_STATUTORY"; }
			return line;
		}
		var earnings = rules.Where(x => x.LineKind == SalaryTemplateLineKind.Earning).OrderBy(x => x.DisplayOrder).Select(Map).ToList();
		var deductions = rules.Where(x => x.LineKind == SalaryTemplateLineKind.Deduction &&
			(x.SemanticType != PayrollComponentSemanticType.EmployeeProvidentFund || pfRule != null))
			.OrderBy(x => x.DisplayOrder).Select(Map).ToList();
		CalculatedSalaryLineDto SystemLine(string code, string name, decimal amount, bool esi) => new()
		{
			ComponentId = esi ? esiSource!.ComponentId!.Value : rules.Single(x => x.SemanticType == PayrollComponentSemanticType.EmployeeProvidentFund).ComponentId,
			Code = code, Name = name, Amount = amount,
			SemanticType = esi ? "EMPLOYEE_STATE_INSURANCE" : "EMPLOYEE_PROVIDENT_FUND",
			FormulaType = esi ? "EMPLOYEE_STATE_INSURANCE" : "EMPLOYEE_PROVIDENT_FUND",
			IsStatutory = true, Persistable = false, LineSource = "SYSTEM_STATUTORY"
		};
		if (result.EmployeeStateInsurance is { IsCovered: true, CanCalculate: true } esiResult)
			deductions.Add(SystemLine("EMPLOYEE_ESI", esiSource!.Name, esiResult.EmployeeContribution, true));
		var employer = result.EmployerContributions.Select(x => SystemLine(
			x.RuleId == pfRule?.Id ? "EMPLOYER_PF" : "EMPLOYER_ESI", x.Name, x.Amount, x.RuleId != pfRule?.Id)).ToList();
		return new SalaryTemplateEvaluation(gross, result.BasicSalary, result.TotalDeduction, result.NetPay,
			result.EmployerContributionTotal, earnings, deductions, employer,
			earnings.Concat(deductions).Select(x => $"{x.Name} = {x.Amount:0.00}").ToList(), result.EmployeeStateInsurance);
	}

	private static void AddEsiCoverageBreakpoints(
		ISet<decimal> breakpoints,
		decimal upperBound,
		IReadOnlyList<SalaryTemplateRule> rules,
		IReadOnlyDictionary<Guid, SalaryTemplateComponent> components,
		ProvidentFund? pfRule,
		IReadOnlyList<ProfessionalTax> slabs,
		SalaryTemplateEsiEvaluationContext? esiContext)
	{
		if (esiContext == null || upperBound <= 0.01m)
		{
			return;
		}

		const int samples = 128;
		decimal? previousGross = null;
		bool? previousCoverage = null;
		for (var index = 0; index <= samples; index++)
		{
			var gross = Round(0.01m + (upperBound - 0.01m) * index / samples);
			var evaluation = TryEvaluateGross(gross, rules, components, pfRule, slabs, esiContext);
			if (evaluation?.EmployeeStateInsurance == null)
			{
				continue;
			}

			var coverage = evaluation.EmployeeStateInsurance.IsCovered;
			if (previousCoverage.HasValue && previousCoverage.Value != coverage && previousGross.HasValue)
			{
				var low = ToPaise(previousGross.Value);
				var high = ToPaise(gross);
				while (low < high)
				{
					var mid = low + (high - low) / 2;
					var midEvaluation = TryEvaluateGross(FromPaise(mid), rules, components, pfRule, slabs, esiContext);
					if (midEvaluation?.EmployeeStateInsurance?.IsCovered == previousCoverage.Value)
					{
						low = mid + 1;
					}
					else
					{
						high = mid;
					}
				}

				AddBreakpoint(breakpoints, FromPaise(low), upperBound);
			}

			previousGross = gross;
			previousCoverage = coverage;
		}
	}

	private static SalaryTemplateEvaluation? TryEvaluateGross(
		decimal gross,
		IReadOnlyList<SalaryTemplateRule> rules,
		IReadOnlyDictionary<Guid, SalaryTemplateComponent> components,
		ProvidentFund? pfRule,
		IReadOnlyList<ProfessionalTax> slabs,
		SalaryTemplateEsiEvaluationContext? esiContext,
		bool allowUnallocated = false)
	{
		try
		{
			return EvaluateGross(gross, rules, components, pfRule, slabs, esiContext: esiContext, allowUnallocated: allowUnallocated);
		}
		catch (BusinessException exception) when (exception.Code is "HR:Payroll:SalaryTemplateEarningsExceedGross" or HRErrorCodes.SalaryTemplateAllocationMismatch)
		{
			return null;
		}
	}

	private static void AddBreakpoint(ISet<decimal> breakpoints, decimal value, decimal upperBound)
	{
		var rounded = Round(value);
		if (rounded >= 0.01m && rounded <= upperBound)
		{
			breakpoints.Add(rounded);
		}
	}

	private static long ToPaise(decimal value) => decimal.ToInt64(Round(value) * 100m);

	private static decimal FromPaise(long value) => value / 100m;

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
		FormulaValue = rule.FormulaType == SalaryTemplateFormulaType.FixedAmount && rule.Value.HasValue ? Round(rule.Value.Value) : rule.Value,
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
