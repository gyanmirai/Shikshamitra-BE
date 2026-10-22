using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Compensation;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Payroll.Statutory;
using ShikshaMitra.HR.Payroll.Templates;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class EsiPolicyRepricing_Tests
{
	[Fact]
	public void A_policy_version_change_reallocates_protected_ctc_without_rewriting_the_saved_salary()
	{
		var employee = Guid.NewGuid(); var basic = Guid.NewGuid(); var policy = Guid.NewGuid();
		var rules = new[] { new SalaryTemplateRule("BASIC", basic, SalaryTemplateLineKind.Earning,
			SalaryTemplateFormulaType.PercentageOfGross, PayrollComponentSemanticType.Basic, 100m, null, 0) };
		var establishment = new StatutoryEstablishment(Guid.NewGuid(), null, "ESI", "IN-GJ");
		establishment.Configure("IN-GJ", "Test", 10, new(2026, 1, 1), new(2026, 1, 1), null);
		establishment.EnablePlatformCalculation();
		var oldRule = Rule(policy, 1, .75m, 3.25m);
		var settings = new EsiCalculationSettings { EligibilityBasis = "GROSS", CalculationBasis = "GROSS" };
		var oldContext = new SalaryTemplateEsiEvaluationContext(new(2026, 4, 1), oldRule, establishment, null, new Dictionary<Guid, StatutoryWageTreatment>(), [], Guid.NewGuid(), "ESIC") { CalculationSettings = settings };
		var old = SalaryTemplateCalculator.Calculate(SalaryInputBasis.EmployerCost, 10000m, rules,
			new Dictionary<Guid, SalaryTemplateComponent> { [basic] = new(basic, "Basic Salary") }, null, [], oldContext);
		var snapshot = JsonSerializer.Serialize(new SalaryTemplateCalculationDto { GrossSalary = old.Gross,
			Earnings = old.Earnings, Deductions = old.Deductions, EmployeeStateInsurance = new() { RuleId = oldRule.Id } });
		var revision = new EmployeeCompensationRevision(Guid.NewGuid(), null, employee, 1, new(2026, 4, 1));
		revision.ReplaceDraft(new(2026, 4, 1), SalaryInputBasis.EmployerCost, CompensationProtectionPolicy.ProtectEmployerCost,
			10000m, 10000m, old.Gross, old.Net, old.EmployerCost, null, null, null, null, "IN-GJ", snapshot);
		revision.SetEsiAssignment(EsiManagement.InPlatform, policy, null);
		revision.MakeEffective(Guid.NewGuid(), DateTime.UtcNow);
		var components = new List<EmployeePayrollComponent> { new(Guid.NewGuid(), employee, "Basic Salary", basic, null,
			old.Gross, true, false, null, SalaryComponentType.Fixed, SalaryComponentDependency.None, true, false, PayrollComponentSemanticType.Basic) };
		components[0].Formula = new(SalaryTemplateFormulaType.PercentageOfGross, 100m, null);
		var deduction = new EmployeePayrollComponent(Guid.NewGuid(), employee, "ESIC", Guid.NewGuid(), null, 0m, true, false, null,
			SalaryComponentType.Fixed, SalaryComponentDependency.None, false, false, PayrollComponentSemanticType.EmployeeStateInsurance);
		var current = new PayrollEsiStatutoryContext(new(2026, 5, 1), Rule(policy, 2, 1m, 4m), establishment, null,
			new Dictionary<Guid, StatutoryWageTreatment>(), [], deduction) { CalculationSettings = settings };
		PayrollEsiPolicyCompensationRecalculator.HasPolicyVersionChange(revision, current.Rule.Id).ShouldBeTrue();
		PayrollEsiPolicyCompensationRecalculator.Recalculate(revision, components, rules, current, null, [], null);
		var result = PayrollComputation.Calculate(1, 0, 1m, 1m, components, new PayrollStatutoryRuleSet(null, [], EmployeeStateInsurance: current));
		(result.GrossSalary + result.EmployerContributionTotal).ShouldBe(10000m, .01m);
		result.GrossSalary.ShouldBeLessThan(old.Gross);
		revision.GrossSalary.ShouldBe(old.Gross);
		revision.CalculationSnapshotJson.ShouldBe(snapshot);
		revision.Status.ShouldBe(CompensationRevisionStatus.Approved);
	}
	private static StatutoryRuleVersion Rule(Guid policy, int version, decimal employee, decimal employer) => new(Guid.NewGuid(), null,
		"ESI", "IN-GJ", version, new(2026, 1, 1), null, 10, 21000m, 25000m, employee, employer, 50m, 15m,
		StatutoryRoundingPolicy.NextHigherRupee, "https://example.invalid/test", new(2026, 1, 1), esiPolicyId: policy);
}
