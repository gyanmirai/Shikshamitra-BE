using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Templates;
using Volo.Abp;

namespace ShikshaMitra.HR.Payroll.Services;

public static class PayrollSalaryProtection
{
	public static PayrollComputationResult Calculate(int presentDays, int halfWorkingDays, decimal payableDays,
		decimal basisDays, IReadOnlyList<EmployeePayrollComponent> components, PayrollStatutoryRuleSet? statutoryRules,
		CompensationProtectionPolicy policy, decimal monthlyTarget)
	{
		var recurring = components.Where(x => !x.IsTemp).ToList();
		var baseline = PayrollComputation.Calculate(presentDays, halfWorkingDays, payableDays, basisDays, recurring, statutoryRules);
		var target = GrossEarningAllocation.Money(monthlyTarget * baseline.ProrationFactor);
		var takeHome = policy == CompensationProtectionPolicy.ProtectTakeHome;
		decimal Achieved(PayrollComputationResult value) => policy switch
		{
			CompensationProtectionPolicy.ProtectTakeHome => value.StatutoryTakeHome,
			CompensationProtectionPolicy.ProtectEmployerCost => value.GrossSalary + value.EmployerContributionTotal,
			_ => value.GrossSalary
		};
		var solved = baseline;
		decimal? adjustedGross = null;
		var status = policy == CompensationProtectionPolicy.ProtectGross ? "LEGACY" : "UNCHANGED";
		var hasMissingRules = recurring.Any(x => x.SemanticType == PayrollComponentSemanticType.EmployeeProvidentFund) && statutoryRules?.ProvidentFund is not { IsActive: true }
			|| recurring.Any(x => x.SemanticType == PayrollComponentSemanticType.ProfessionalTax) && statutoryRules?.ProfessionalTaxSlabs.Any(x => x.IsActive) != true
			|| baseline.EmployeeStateInsurance is { CanCalculate: false };
		if (hasMissingRules) status = "BLOCKED";
		else if (policy != CompensationProtectionPolicy.ProtectGross && Math.Abs(Achieved(baseline) - target) > 0.01m)
		{
			try
			{
				var maximum = takeHome ? Math.Min(100_000_000m, target * 2m + 1_000_000m) : target;
				var rules = recurring.Where(x => x.IsEarning).Select(x => new GrossEarningRule(
					x.ComponentId ?? x.Id, x.SemanticType == PayrollComponentSemanticType.Basic,
					x.Formula?.Type ?? SalaryTemplateFormulaType.FixedAmount, x.Formula?.Value ?? x.Amount,
					x.Formula?.BasisComponentId)).ToList();
				var (minimum, upper) = CompensationTargetSolver.GrossBounds(rules, maximum, baseline.ProrationFactor);
				PayrollComputationResult? Evaluate(decimal gross, bool probe)
				{
					try { return PayrollComputation.Calculate(presentDays, halfWorkingDays, payableDays, basisDays, recurring, statutoryRules, gross, probe); }
					catch (BusinessException error) when (error.Code is HRErrorCodes.SalaryTemplateAllocationMismatch or "HR:Payroll:SalaryTemplateEarningsExceedGross") { return null; }
				}
				var boundaries = new List<decimal>();
				foreach (var slab in statutoryRules?.ProfessionalTaxSlabs ?? [])
				{
					boundaries.Add(slab.FromSalary);
					if (slab.ToSalary.HasValue) boundaries.Add(slab.ToSalary.Value + 0.01m);
				}
				// Locate the first valid allocation, then the ESI coverage transition.
				if (minimum <= upper && Evaluate(upper, true) != null)
				{
					var low = decimal.ToInt64(minimum * 100m);
					var high = decimal.ToInt64(upper * 100m);
					while (low < high)
					{
						var mid = low + (high - low) / 2;
						if (Evaluate(mid / 100m, true) == null) low = mid + 1; else high = mid;
					}
					minimum = low / 100m;
					var initialCoverage = Evaluate(minimum, true)?.EmployeeStateInsurance?.IsCovered;
					if (initialCoverage.HasValue && Evaluate(upper, true)?.EmployeeStateInsurance?.IsCovered != initialCoverage)
					{
						high = decimal.ToInt64(upper * 100m);
						while (low < high)
						{
							var mid = low + (high - low) / 2;
							if (Evaluate(mid / 100m, true)?.EmployeeStateInsurance?.IsCovered == initialCoverage) low = mid + 1; else high = mid;
						}
						boundaries.Add(low / 100m);
					}
				}
				adjustedGross = CompensationTargetSolver.Solve(target, takeHome, minimum, upper, boundaries,
					(gross, probe) => Evaluate(gross, probe) is { } evaluation ? Achieved(evaluation) : null,
					statutoryRules?.EmployeeStateInsurance == null ? 2 : 200);
				solved = Evaluate(adjustedGross.Value, false)!;
				status = "ADJUSTED";
			}
			catch (BusinessException error) when (error.Code == CompensationTargetSolver.NotSolvable)
			{
				status = "BLOCKED";
			}
		}
		var final = PayrollComputation.Calculate(presentDays, halfWorkingDays, payableDays, basisDays, components, statutoryRules, adjustedGross);
		var achieved = Achieved(solved);
		return final with
		{
			Protection = new SalaryProtectionResult(
				takeHome ? "PROTECT_TAKE_HOME" : policy == CompensationProtectionPolicy.ProtectGross ? "PROTECT_GROSS" : "PROTECT_EMPLOYER_COST",
				monthlyTarget, target, achieved, GrossEarningAllocation.Money(achieved - target), solved.GrossSalary,
				solved.GrossSalary - baseline.GrossSalary,
				final.Deductions.Where(x => x.Source.SemanticType is not (PayrollComponentSemanticType.EmployeeProvidentFund or PayrollComponentSemanticType.ProfessionalTax or PayrollComponentSemanticType.EmployeeStateInsurance)).Sum(x => x.Amount),
				final.AdditionalEarnings,
				(solved.StatutoryTakeHome - final.StatutoryTakeHome),
				final.EmployerContributionTotal - solved.EmployerContributionTotal, status)
		};
	}
}
