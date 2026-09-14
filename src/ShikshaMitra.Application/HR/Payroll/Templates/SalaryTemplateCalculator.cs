using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.Payroll.Settings;
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

		var candidates = new Dictionary<decimal, SalaryTemplateEvaluation>();
		var boundaries = breakpoints.Where(x => x >= allocationMinimum && x <= allocationMaximum).OrderBy(x => x).ToList();
		AddCandidate(candidates, allocationMinimum, rules, components, pfRule, slabs, esiContext);
		for (var index = 0; index < boundaries.Count - 1; index++)
		{
			var low = boundaries[index];
			var high = boundaries[index + 1];
			AddCandidate(candidates, low, rules, components, pfRule, slabs, esiContext);
			AddCandidate(candidates, high, rules, components, pfRule, slabs, esiContext);

			var lowPaise = ToPaise(low);
			var highPaise = ToPaise(high);
			while (lowPaise <= highPaise)
			{
				var midPaise = lowPaise + (highPaise - lowPaise) / 2;
				var midGross = FromPaise(midPaise);
				var evaluation = TryEvaluateGross(midGross, rules, components, pfRule, slabs, esiContext, allowUnallocated: true);
				if (evaluation == null)
				{
					lowPaise = midPaise + 1;
					continue;
				}

				AddCandidate(candidates, midGross, rules, components, pfRule, slabs, esiContext);
				var achieved = inputBasis == SalaryInputBasis.TakeHome ? evaluation.Net : evaluation.EmployerCost;
				if (inputBasis == SalaryInputBasis.TakeHome)
				{
					if (achieved >= target)
					{
						highPaise = midPaise - 1;
					}
					else
					{
						lowPaise = midPaise + 1;
					}
				}
				else if (achieved <= target)
				{
					lowPaise = midPaise + 1;
				}
				else
				{
					highPaise = midPaise - 1;
				}
			}

			// Next-higher-rupee ESI creates small downward steps in take-home pay.
			// Check both sides rather than assuming a globally monotone cent-level function.
			var roundingWindow = esiContext == null ? 2 : 200;
			for (var paise = Math.Max(ToPaise(low), highPaise - roundingWindow); paise <= Math.Min(ToPaise(high), lowPaise + roundingWindow); paise++)
			{
				AddCandidate(candidates, FromPaise(paise), rules, components, pfRule, slabs, esiContext);
			}
		}

		var feasible = candidates.Values.Where(evaluation => inputBasis == SalaryInputBasis.TakeHome
			? evaluation.Net >= target
			: evaluation.EmployerCost <= target).ToList();
		if (!rules.Any(x => x.FormulaType == SalaryTemplateFormulaType.RemainderOfGross))
		{
			feasible = feasible.Where(x => Math.Abs(Round((inputBasis == SalaryInputBasis.TakeHome ? x.Net : x.EmployerCost) - target)) <= 0.01m).ToList();
		}
		if (feasible.Count == 0)
		{
			throw new BusinessException("HR:Payroll:CompensationTargetNotSolvable")
				.WithData("salaryBasis", inputBasis.ToString())
				.WithData("target", target)
				.WithData("message", "This target cannot be reached with earnings that reconcile to gross. Change the target or the template's earning rules.");
		}

		return inputBasis == SalaryInputBasis.TakeHome
			? feasible.OrderBy(x => x.Gross).ThenBy(x => x.Net - target).First().Gross
			: feasible.OrderByDescending(x => x.Gross).ThenBy(x => target - x.EmployerCost).First().Gross;
	}

	private static SalaryTemplateEvaluation EvaluateGross(
		decimal gross,
		IReadOnlyList<SalaryTemplateRule> rules,
		IReadOnlyDictionary<Guid, SalaryTemplateComponent> components,
		ProvidentFund? pfRule,
		IReadOnlyList<ProfessionalTax> professionalTaxSlabs,
		decimal? forcedProfessionalTax = null,
		SalaryTemplateEsiEvaluationContext? esiContext = null,
		bool allowUnallocated = false)
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

		var remainderRule = orderedRules.SingleOrDefault(x => x.LineKind == SalaryTemplateLineKind.Earning && x.FormulaType == SalaryTemplateFormulaType.RemainderOfGross);
		var remainder = Round(gross - earnings.Sum(x => x.Amount));
		if (remainderRule == null && remainder != 0m && !allowUnallocated)
		{
			throw new BusinessException(HRErrorCodes.SalaryTemplateAllocationMismatch)
				.WithData("gross", gross).WithData("allocated", earnings.Sum(x => x.Amount))
				.WithData("difference", remainder)
				.WithData("message", FormattableString.Invariant($"Earnings must exactly equal gross salary. Gross: INR {gross:0.00}; allocated earnings: INR {earnings.Sum(x => x.Amount):0.00}; difference: INR {remainder:0.00}. Adjust the earning rules or explicitly choose a balancing earning."));
		}
		if (remainderRule != null && remainder < 0m)
		{
			throw new BusinessException("HR:Payroll:SalaryTemplateEarningsExceedGross").WithData("gross", gross);
		}

		if (remainderRule != null)
		{
			values[remainderRule.Code] = remainder;
			earnings.Add(ToLine(remainderRule, components, remainder));
			trace.Add($"{GetName(remainderRule, components)} = gross remainder = {remainder:0.00}");
		}
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
						IsStatutory = true,
						LineSource = "SYSTEM_STATUTORY",
						Persistable = false
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
			if (line.IsStatutory)
			{
				line.LineSource = "SYSTEM_STATUTORY";
				line.Persistable = false;
			}
			deductions.Add(line);
			values[rule.Code] = amount;
		}

		EsiStatutoryCalculationResult? esiResult = null;
		if (esiContext != null)
		{
			var wageLines = earnings.Select(line => new StatutoryWageLine(
				line.ComponentId,
				line.Name,
				line.Amount,
				esiContext.ComponentTreatments.GetValueOrDefault(line.ComponentId, StatutoryWageTreatment.Unclassified)))
				.ToList();
			esiResult = EsiStatutoryCalculator.Calculate(
				esiContext.EffectiveMonth,
				esiContext.Rule,
				esiContext.Establishment,
				esiContext.Enrollment,
				wageLines,
				esiContext.Overrides);
			if (esiResult.IsCovered && esiResult.CanCalculate)
			{
				deductions.Add(new CalculatedSalaryLineDto
				{
					ComponentId = esiContext.DeductionComponentId,
					Code = "EMPLOYEE_ESI",
					Name = esiContext.DeductionComponentName,
					Amount = esiResult.EmployeeContribution,
					SemanticType = "EMPLOYEE_STATE_INSURANCE",
					FormulaType = "EMPLOYEE_STATE_INSURANCE",
					IsStatutory = true,
					LineSource = "SYSTEM_STATUTORY",
					Persistable = false
				});
				employerContributions.Add(new CalculatedSalaryLineDto
				{
					ComponentId = esiContext.DeductionComponentId,
					Code = "EMPLOYER_ESI",
					Name = "Employer State Insurance (ESI)",
					Amount = esiResult.EmployerContribution,
					SemanticType = "EMPLOYEE_STATE_INSURANCE",
					FormulaType = "EMPLOYEE_STATE_INSURANCE",
					IsStatutory = true,
					LineSource = "SYSTEM_STATUTORY",
					Persistable = false
				});
				trace.Add($"Employee ESI = {esiResult.EmployeeContribution:0.00}; employer ESI = {esiResult.EmployerContribution:0.00}");
			}
		}

		var totalDeductions = Round(deductions.Sum(x => x.Amount));
		var displayOrderByCode = rules.ToDictionary(x => x.Code, x => x.DisplayOrder, StringComparer.OrdinalIgnoreCase);
		return new SalaryTemplateEvaluation(
			gross,
			basic,
			totalDeductions,
			Round(gross - totalDeductions),
			Round(employerContributions.Sum(x => x.Amount)),
			earnings.OrderBy(x => displayOrderByCode.GetValueOrDefault(x.Code, int.MaxValue)).ToList(),
			deductions.OrderBy(x => displayOrderByCode.GetValueOrDefault(x.Code, int.MaxValue)).ToList(),
			employerContributions,
			trace,
			esiResult);
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

	private static void AddCandidate(
		IDictionary<decimal, SalaryTemplateEvaluation> candidates,
		decimal gross,
		IReadOnlyList<SalaryTemplateRule> rules,
		IReadOnlyDictionary<Guid, SalaryTemplateComponent> components,
		ProvidentFund? pfRule,
		IReadOnlyList<ProfessionalTax> slabs,
		SalaryTemplateEsiEvaluationContext? esiContext)
	{
		var evaluation = TryEvaluateGross(gross, rules, components, pfRule, slabs, esiContext);
		if (evaluation != null)
		{
			candidates[gross] = evaluation;
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
