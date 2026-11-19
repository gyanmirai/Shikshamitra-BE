using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.Templates;
using Volo.Abp;

namespace ShikshaMitra.HR.Payroll.Services;

public static class PayrollSalaryProtection
{
	public static PayrollComputationResult Calculate(int presentDays, int halfWorkingDays, decimal payableDays,
		decimal basisDays, IReadOnlyList<EmployeePayrollComponent> components, PayrollStatutoryRuleSet? statutoryRules,
		decimal monthlyTarget)
	{
		var recurring = components.Where(x => !x.IsTemp).ToList();
		var factor = PayrollProrationCalculator.CalculateProrationFactor(basisDays, payableDays);
		var baselineGross = recurring.Where(x => x.IsEarning).Sum(x => x.Amount) * factor;
		var baseline = PayrollComputation.Calculate(
			presentDays, halfWorkingDays, payableDays, basisDays, recurring, statutoryRules, baselineGross);
		var target = GrossEarningAllocation.Money(monthlyTarget * baseline.ProrationFactor);
		static decimal EmployerCost(PayrollComputationResult value) => value.GrossSalary + value.EmployerContributionTotal;
		var solved = baseline;
		decimal? adjustedGross = null;
		var status = "UNCHANGED";
		var hasMissingRules = recurring.Any(x => x.SemanticType == PayrollComponentSemanticType.EmployeeProvidentFund) && statutoryRules?.ProvidentFund is not { IsActive: true }
			|| baseline.EsiCalculation is { CanCalculate: false }
			|| baseline.StatutoryIssueCodes.Any(PayrollLockPreconditionGuard.IsBlockingLockIssue);
		if (hasMissingRules) status = "BLOCKED";
		else if (Math.Abs(EmployerCost(baseline) - target) > 0.01m)
		{
			try
			{
				var rules = recurring.Where(x => x.IsEarning).Select(x => new GrossEarningRule(
					x.ComponentId ?? x.Id, x.SemanticType == PayrollComponentSemanticType.Basic,
					x.Formula?.Type ?? SalaryTemplateFormulaType.FixedAmount, x.Formula?.Value ?? x.Amount,
					x.Formula?.BasisComponentId)).ToList();
				var (minimum, upper) = CompensationTargetSolver.GrossBounds(rules, target, baseline.ProrationFactor);
				PayrollComputationResult? Evaluate(decimal gross, bool probe)
				{
					try { return PayrollComputation.Calculate(presentDays, halfWorkingDays, payableDays, basisDays, recurring, statutoryRules, gross, probe); }
					catch (BusinessException error) when (error.Code is HRErrorCodes.SalaryTemplateAllocationMismatch or "HR:Payroll:SalaryTemplateEarningsExceedGross") { return null; }
				}
				var boundaries = new List<decimal>();
				// Locate the first valid allocation, then the ESI coverage transition.
				var upperEvaluation = minimum < upper ? Evaluate(upper, true) : null;
				if (upperEvaluation != null)
				{
					var low = decimal.ToInt64(minimum * 100m);
					var high = decimal.ToInt64(upper * 100m);
					var first = Evaluate(minimum, true);
					if (first == null)
					{
						while (low < high)
						{
							var mid = low + (high - low) / 2;
							if (Evaluate(mid / 100m, true) == null) low = mid + 1; else high = mid;
						}
						minimum = low / 100m;
						first = Evaluate(minimum, true);
					}
					var initialCoverage = first?.EsiCalculation?.IsCovered;
					if (initialCoverage.HasValue && upperEvaluation.EsiCalculation?.IsCovered != initialCoverage)
					{
						high = decimal.ToInt64(upper * 100m);
						while (low < high)
						{
							var mid = low + (high - low) / 2;
							if (Evaluate(mid / 100m, true)?.EsiCalculation?.IsCovered == initialCoverage) low = mid + 1; else high = mid;
						}
						boundaries.Add(low / 100m);
					}
				}
				var candidate = CompensationTargetSolver.Solve(target, minimum, upper, boundaries, (gross, probe) =>
				{
					var result = Evaluate(gross, probe);
					return result != null && result.EsiCalculation is not { CanCalculate: false } &&
						!result.StatutoryIssueCodes.Any(PayrollLockPreconditionGuard.IsBlockingLockIssue)
						? EmployerCost(result) : null;
				});
				solved = Evaluate(candidate, false)!;
				adjustedGross = candidate;
				status = "ADJUSTED";
			}
			catch (BusinessException error) when (error.Code == CompensationTargetSolver.NotSolvable)
			{
				status = "BLOCKED";
			}
		}
		var final = recurring.Count == components.Count ? solved : PayrollComputation.Calculate(
			presentDays, halfWorkingDays, payableDays, basisDays, components, statutoryRules, adjustedGross ?? baselineGross);
		var achieved = EmployerCost(solved);
		return final with
		{
			Protection = new SalaryProtectionResult(
				"PROTECT_EMPLOYER_COST",
				monthlyTarget, target, achieved, GrossEarningAllocation.Money(achieved - target), solved.GrossSalary,
				solved.GrossSalary - baseline.GrossSalary,
				final.Deductions.Where(x => x.Source.SemanticType is not (PayrollComponentSemanticType.EmployeeProvidentFund or PayrollComponentSemanticType.ProfessionalTax or PayrollComponentSemanticType.EmployeeStateInsurance)).Sum(x => x.Amount),
				final.AdditionalEarnings,
				EmployeeStatutoryDeductions(final) - EmployeeStatutoryDeductions(solved),
				final.EmployerContributionTotal - solved.EmployerContributionTotal, status)
		};
	}

	private static decimal EmployeeStatutoryDeductions(PayrollComputationResult calculation) =>
		calculation.Deductions.Where(x => x.Source.SemanticType is PayrollComponentSemanticType.EmployeeProvidentFund
			or PayrollComponentSemanticType.ProfessionalTax or PayrollComponentSemanticType.EmployeeStateInsurance).Sum(x => x.Amount);
}
