using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.HR.Payroll.Statutory;

namespace ShikshaMitra.HR.Payroll.Services;

public static class PayrollComputation
{
	public static PayrollComputationResult Calculate(
		int presentDays,
		int halfWorkingDays,
		decimal basisDays,
		IReadOnlyList<EmployeePayrollComponent> components)
	{
		var payableDays = PayrollProrationCalculator.CalculatePayableDays(presentDays, halfWorkingDays);
		return Calculate(presentDays, halfWorkingDays, payableDays, basisDays, components);
	}

	public static PayrollComputationResult Calculate(
		int presentDays,
		int halfWorkingDays,
		decimal payableDays,
		decimal basisDays,
		IReadOnlyList<EmployeePayrollComponent> components,
		PayrollStatutoryRuleSet? statutoryRules = null)
	{
		var factor = PayrollProrationCalculator.CalculateProrationFactor(basisDays, payableDays);
		var deductions = new List<PayrollComputedLineItem>();
		var fullGrossSalary = components.Where(c => c.IsEarning && !c.IsTemp).Sum(c => c.Amount);
		var grossSalary = Round(fullGrossSalary * factor);
		var calculator = new PayrollOrdinaryComponentCalculator(components, grossSalary, factor);
		var earnings = calculator.CalculateEarnings();
		var totalEarnings = earnings.Sum(x => x.Amount);
		var basicSalary = earnings.Where(x => !x.Source.IsTemp &&
			(x.Source.SemanticType == Templates.PayrollComponentSemanticType.Basic || IsBasicSalaryComponentName(x.Source.Name)))
			.Sum(x => x.Amount);

		foreach (var component in components.Where(x => !x.IsEarning &&
			x.SemanticType is not (Templates.PayrollComponentSemanticType.EmployeeProvidentFund or
				Templates.PayrollComponentSemanticType.ProfessionalTax or
				Templates.PayrollComponentSemanticType.EmployeeStateInsurance)))
		{
			var amount = calculator.Calculate(component);
			deductions.Add(new PayrollComputedLineItem(component, amount));
		}

		var employerContributions = new List<PayrollComputedEmployerContribution>();
		Guid? pfRuleId = null;
		int? pfRuleVersion = null;
		var pfSource = components.FirstOrDefault(x =>
			!x.IsEarning && x.SemanticType == Templates.PayrollComponentSemanticType.EmployeeProvidentFund);
		var pfRule = statutoryRules?.ProvidentFund;
		if (pfSource != null && pfRule is { IsActive: true })
		{
			var contributionBase = basicSalary;
			if (pfRule.ContributionBasis == PfContributionBasis.BasicAndDearnessAllowance)
			{
				contributionBase += earnings
					.Where(x => x.Source.SemanticType == Templates.PayrollComponentSemanticType.DearnessAllowance)
					.Sum(x => x.Amount);
			}
			if (contributionBase <= 0m)
			{
				contributionBase = totalEarnings;
			}
			if (pfRule.WageCeiling.HasValue)
			{
				contributionBase = Math.Min(contributionBase, pfRule.WageCeiling.Value);
			}

			var employeePf = Round(contributionBase * pfRule.EmployeePercentage / 100m);
			if (employeePf > 0m)
			{
				deductions.Add(new PayrollComputedLineItem(pfSource, employeePf));
			}

			var employerPf = Round(contributionBase * pfRule.EmployerPercentage / 100m);
			if (employerPf > 0m)
			{
				employerContributions.Add(new PayrollComputedEmployerContribution(
					$"Employer Provident Fund - {pfRule.Name}", employerPf, pfRule.Id, pfRule.Version));
			}

			pfRuleId = pfRule.Id;
			pfRuleVersion = pfRule.Version;
		}

		Guid? ptRuleId = null;
		int? ptRuleVersion = null;
		var ptSource = components.FirstOrDefault(x =>
			!x.IsEarning && x.SemanticType == Templates.PayrollComponentSemanticType.ProfessionalTax);
		if (ptSource != null)
		{
			var slab = statutoryRules?.ProfessionalTaxSlabs
				.Where(x => x.IsActive &&
					(!statutoryRules.IsEmploymentCessationMonth || x.DeductInCessationMonth) &&
					totalEarnings >= x.FromSalary &&
					(!x.ToSalary.HasValue || totalEarnings <= x.ToSalary.Value))
				.OrderByDescending(x => x.EffectiveFrom)
				.ThenByDescending(x => x.Version)
				.FirstOrDefault();
			if (slab != null)
			{
				deductions.Add(new PayrollComputedLineItem(ptSource, Round(slab.TaxAmount)));
				ptRuleId = slab.Id;
				ptRuleVersion = slab.Version;
			}
		}

		Guid? esiRuleId = null;
		int? esiRuleVersion = null;
		var esiWages = 0m;
		var esiGovernmentContribution = 0m;
		string? esiCoverageDecision = null;
		string? esiInsuranceNumber = null;
		DateTime? esiContributionPeriodStart = null;
		DateTime? esiContributionPeriodEnd = null;
		var statutoryIssueCodes = new List<string>();
		var esiContext = statutoryRules?.EmployeeStateInsurance;
		if (esiContext != null)
		{
			var wageLines = earnings
				.Select(x => new StatutoryWageLine(
					x.Source.ComponentId ?? x.Source.Id,
					x.Source.Name,
					x.Amount,
					esiContext.ComponentTreatments.TryGetValue(x.Source.ComponentId ?? x.Source.Id, out var treatment)
						? treatment
						: StatutoryWageTreatment.Unclassified))
				.ToList();
			var monthlyWageLines = wageLines.Select((line, index) =>
				line with { Amount = earnings[index].Source.Amount }).ToList();
			var esiResult = EsiStatutoryCalculator.Calculate(
				esiContext.PayrollMonth,
				esiContext.Rule,
				esiContext.Establishment,
				esiContext.Enrollment,
				wageLines,
				esiContext.Overrides,
				coverageWageLines: monthlyWageLines);

			esiRuleId = esiContext.Rule.Id;
			esiRuleVersion = esiContext.Rule.Version;
			esiWages = esiResult.StatutoryWages;
			esiGovernmentContribution = esiResult.GovernmentContribution;
			esiCoverageDecision = esiResult.CoverageDecision;
			esiInsuranceNumber = esiContext.Enrollment?.InsuranceNumber;
			esiContributionPeriodStart = esiResult.ContributionPeriodStart;
			esiContributionPeriodEnd = esiResult.ContributionPeriodEnd;
			statutoryIssueCodes.AddRange(esiResult.IssueCodes);

			if (esiResult.IsCovered && esiResult.CanCalculate)
			{
				if (esiResult.EmployeeContribution > 0m)
				{
					deductions.Add(new PayrollComputedLineItem(
						esiContext.DeductionSource,
						esiResult.EmployeeContribution));
				}
				if (esiResult.EmployerContribution > 0m)
				{
					employerContributions.Add(new PayrollComputedEmployerContribution(
						"Employer State Insurance (ESI)",
						esiResult.EmployerContribution,
						esiContext.Rule.Id,
						esiContext.Rule.Version));
				}
			}
		}

		var totalDeduction = deductions.Sum(x => x.Amount);
		var employerContributionTotal = employerContributions.Sum(x => x.Amount);

		return new PayrollComputationResult(
			presentDays,
			halfWorkingDays,
			payableDays,
			factor,
			basicSalary,
			fullGrossSalary,
			grossSalary,
			totalDeduction,
			employerContributionTotal,
			totalEarnings - totalDeduction,
			earnings,
			deductions,
			employerContributions,
			pfRuleId,
			pfRuleVersion,
			ptRuleId,
			ptRuleVersion,
			esiRuleId,
			esiRuleVersion,
			esiWages,
			esiGovernmentContribution,
			esiCoverageDecision,
			esiInsuranceNumber,
			esiContributionPeriodStart,
			esiContributionPeriodEnd,
			statutoryIssueCodes);
	}

	public static SalarySlipLineItem ToSalarySlipLineItem(PayrollComputedLineItem item) =>
		new(
			item.Source.Name,
			item.Amount,
			item.Source.Description,
			item.Source.IsFixed,
			item.Source.IsTemp,
			item.Source.ComponentType,
			item.Source.DependOn ?? SalaryComponentDependency.None)
		{
			FullMonthAmount = item.Source.IsTemp ? null : item.Source.Amount,
			FormulaType = item.Source.Formula?.Type.ToString(),
			FormulaValue = item.Source.Formula?.Value,
			BasisComponentId = item.Source.Formula?.BasisComponentId
		};

	private static bool IsBasicSalaryComponentName(string? name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return false;
		}

		return string.Equals(name, "Basic", StringComparison.OrdinalIgnoreCase)
			|| name.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase);
	}

	private static decimal Round(decimal value) =>
		Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
