using System;
using System.Collections.Generic;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Payroll.Statutory;
using ShikshaMitra.HR.Payroll.Templates;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class EsiStatutoryCalculator_Tests
{
	[Fact]
	public void Should_calculate_employee_and_employer_contributions_with_next_rupee_rounding()
	{
		var result = EsiStatutoryCalculator.Calculate(
			new DateTime(2026, 8, 1),
			BuildRule(),
			BuildActiveEstablishment(),
			BuildEnrollment(EmployeeStatutoryEnrollmentStatus.Covered, insuranceNumber: "ESI-1001"),
			[new StatutoryWageLine(Guid.NewGuid(), "ESI wages", 15_897m, StatutoryWageTreatment.Included)]);

		result.IsCovered.ShouldBeTrue();
		result.StatutoryWages.ShouldBe(15_897m);
		result.EmployeeContribution.ShouldBe(120m);
		result.EmployerContribution.ShouldBe(517m);
		result.ContributionPeriodStart.ShouldBe(new DateTime(2026, 4, 1));
		result.ContributionPeriodEnd.ShouldBe(new DateTime(2026, 9, 30));
		result.IssueCodes.ShouldBeEmpty();
	}

	[Fact]
	public void Should_not_cover_new_standard_employee_above_wage_ceiling()
	{
		var result = EsiStatutoryCalculator.Calculate(
			new DateTime(2026, 8, 1),
			BuildRule(),
			BuildActiveEstablishment(),
			BuildEnrollment(EmployeeStatutoryEnrollmentStatus.NotCovered),
			[new StatutoryWageLine(Guid.NewGuid(), "Salary", 21_001m, StatutoryWageTreatment.Included)]);

		result.IsCovered.ShouldBeFalse();
		result.CoverageDecision.ShouldBe("ABOVE_WAGE_CEILING");
		result.EmployeeContribution.ShouldBe(0m);
		result.EmployerContribution.ShouldBe(0m);
	}

	[Fact]
	public void Should_continue_coverage_until_contribution_period_end_after_wages_cross_ceiling()
	{
		var enrollment = BuildEnrollment(EmployeeStatutoryEnrollmentStatus.Covered, "ESI-1001");
		enrollment.Update(
			EmployeeStatutoryEnrollmentStatus.Covered,
			StatutoryWageCeilingCategory.Standard,
			"ESI-1001",
			new DateTime(2026, 4, 1),
			null,
			new DateTime(2026, 9, 30),
			"Coverage continues for the contribution period");

		var result = EsiStatutoryCalculator.Calculate(
			new DateTime(2026, 8, 1),
			BuildRule(),
			BuildActiveEstablishment(),
			enrollment,
			[new StatutoryWageLine(Guid.NewGuid(), "Salary", 28_500m, StatutoryWageTreatment.Included)]);

		result.IsCovered.ShouldBeTrue();
		result.CoverageDecision.ShouldBe("CONTRIBUTION_PERIOD_CONTINUATION");
		result.EmployeeContribution.ShouldBe(214m);
		result.EmployerContribution.ShouldBe(927m);
	}

	[Fact]
	public void Should_not_continue_above_ceiling_without_a_recorded_contribution_period_end()
	{
		var result = EsiStatutoryCalculator.Calculate(
			new DateTime(2026, 8, 1),
			BuildRule(),
			BuildActiveEstablishment(),
			BuildEnrollment(EmployeeStatutoryEnrollmentStatus.Covered, "ESI-1001"),
			[new StatutoryWageLine(Guid.NewGuid(), "Salary", 28_500m, StatutoryWageTreatment.Included)]);

		result.IsCovered.ShouldBeFalse();
		result.CoverageDecision.ShouldBe("ABOVE_WAGE_CEILING");
		result.EmployeeContribution.ShouldBe(0m);
		result.EmployerContribution.ShouldBe(0m);
	}

	[Fact]
	public void Should_use_disability_ceiling_and_apply_statutory_wage_classifications()
	{
		var enrollment = BuildEnrollment(EmployeeStatutoryEnrollmentStatus.Covered, "ESI-1002");
		enrollment.Update(
			EmployeeStatutoryEnrollmentStatus.Covered,
			StatutoryWageCeilingCategory.PersonWithDisability,
			"ESI-1002",
			new DateTime(2026, 4, 1),
			null,
			null,
			null);

		var result = EsiStatutoryCalculator.Calculate(
			new DateTime(2026, 8, 1),
			BuildRule(),
			BuildActiveEstablishment(),
			enrollment,
			[
				new StatutoryWageLine(Guid.NewGuid(), "Basic", 10_000m, StatutoryWageTreatment.Included),
				new StatutoryWageLine(Guid.NewGuid(), "Specified exclusion", 15_000m, StatutoryWageTreatment.SpecifiedExclusion),
				new StatutoryWageLine(Guid.NewGuid(), "In kind", 5_000m, StatutoryWageTreatment.InKind)
			]);

		result.WageCeiling.ShouldBe(25_000m);
		result.TotalRemuneration.ShouldBe(30_000m);
		result.ExclusionAddBack.ShouldBe(0m);
		result.InKindWages.ShouldBe(4_500m);
		result.StatutoryWages.ShouldBe(14_500m);
	}

	[Fact]
	public void Should_stop_calculation_when_an_earning_component_is_unclassified()
	{
		var result = EsiStatutoryCalculator.Calculate(
			new DateTime(2026, 8, 1),
			BuildRule(),
			BuildActiveEstablishment(),
			null,
			[new StatutoryWageLine(Guid.NewGuid(), "Unknown allowance", 1_000m, StatutoryWageTreatment.Unclassified)]);

		result.CanCalculate.ShouldBeFalse();
		result.IssueCodes.ShouldContain(EsiStatutoryCalculator.WageClassificationIssue);
	}

	[Fact]
	public void Should_add_back_specified_exclusions_above_half_of_total_remuneration()
	{
		var result = EsiStatutoryCalculator.Calculate(
			new DateTime(2026, 8, 1),
			BuildRule(),
			BuildActiveEstablishment(),
			BuildEnrollment(EmployeeStatutoryEnrollmentStatus.Covered, "ESI-1001"),
			[
				new StatutoryWageLine(Guid.NewGuid(), "Basic", 10_000m, StatutoryWageTreatment.Included),
				new StatutoryWageLine(Guid.NewGuid(), "Specified exclusions", 15_000m, StatutoryWageTreatment.SpecifiedExclusion)
			]);

		result.TotalRemuneration.ShouldBe(25_000m);
		result.ExclusionAddBack.ShouldBe(2_500m);
		result.StatutoryWages.ShouldBe(12_500m);
	}

	[Fact]
	public void Payroll_computation_should_add_esi_deduction_and_employer_contribution()
	{
		var employeeId = Guid.NewGuid();
		var basicId = Guid.NewGuid();
		var basic = BuildComponent(employeeId, basicId, "Basic", 9_538m, isEarning: true, PayrollComponentSemanticType.Basic);
		var esiSource = BuildComponent(employeeId, Guid.NewGuid(), "Employee State Insurance (ESI) - Auto", 0m, isEarning: false,
			PayrollComponentSemanticType.EmployeeStateInsurance);
		var result = PayrollComputation.Calculate(
			presentDays: 30,
			halfWorkingDays: 0,
			payableDays: 30m,
			basisDays: 30m,
			[basic, esiSource],
			new PayrollStatutoryRuleSet(
				null,
				[],
				EmployeeStateInsurance: new PayrollEsiStatutoryContext(
					new DateTime(2026, 8, 1),
					BuildRule(),
					BuildActiveEstablishment(),
					BuildEnrollment(EmployeeStatutoryEnrollmentStatus.Covered, "ESI-1001"),
					new Dictionary<Guid, StatutoryWageTreatment> { [basicId] = StatutoryWageTreatment.Included },
					[],
					esiSource)));

		result.Deductions.ShouldContain(x =>
			x.Source.SemanticType == PayrollComponentSemanticType.EmployeeStateInsurance && x.Amount == 72m);
		result.EmployerContributions.ShouldContain(x => x.Name.Contains("ESI") && x.Amount == 310m);
		result.NetPay.ShouldBe(9_466m);
		result.EmployeeStateInsuranceWages.ShouldBe(9_538m);
		result.EmployeeStateInsuranceCoverageDecision.ShouldBe("WITHIN_WAGE_CEILING");
	}

	[Fact]
	public void Payroll_computation_should_classify_employee_specific_earning_by_row_id()
	{
		var employeeId = Guid.NewGuid();
		var rowId = Guid.NewGuid();
		var customEarning = new EmployeePayrollComponent(
			rowId, employeeId, "Employee-specific allowance", null, null, 10_000m,
			isFixed: true, isTemp: false, applicableMonth: null,
			SalaryComponentType.Fixed, SalaryComponentDependency.None,
			isEarning: true, usesMasterValue: false);
		var esiSource = BuildComponent(employeeId, Guid.NewGuid(),
			"Employee State Insurance (ESI) - Auto", 0m, isEarning: false,
			PayrollComponentSemanticType.EmployeeStateInsurance);

		var result = PayrollComputation.Calculate(
			30, 0, 30m, 30m, [customEarning, esiSource],
			new PayrollStatutoryRuleSet(
				null,
				[],
				EmployeeStateInsurance: new PayrollEsiStatutoryContext(
					new DateTime(2026, 8, 1),
					BuildRule(),
					BuildActiveEstablishment(),
					BuildEnrollment(EmployeeStatutoryEnrollmentStatus.Covered, "ESI-1001"),
					new Dictionary<Guid, StatutoryWageTreatment> { [rowId] = StatutoryWageTreatment.Included },
					[],
					esiSource)));

		result.StatutoryIssueCodes.ShouldBeEmpty();
		result.EmployeeStateInsuranceWages.ShouldBe(10_000m);
		result.Deductions.ShouldContain(x =>
			x.Source.SemanticType == PayrollComponentSemanticType.EmployeeStateInsurance && x.Amount == 75m);
	}

	private static StatutoryRuleVersion BuildRule() =>
		new(
			Guid.NewGuid(), Guid.NewGuid(), StatutorySchemeCodes.EmployeeStateInsurance, "IN-GJ", 1,
			new DateTime(2025, 11, 21), null, 10, 21_000m, 25_000m, 0.75m, 3.25m,
			50m, 15m, StatutoryRoundingPolicy.NextHigherRupee,
			"https://www.labour.gov.in/", new DateTime(2026, 8, 18));

	private static StatutoryEstablishment BuildActiveEstablishment()
	{
		var establishment = new StatutoryEstablishment(Guid.NewGuid(), Guid.NewGuid(),
			StatutorySchemeCodes.EmployeeStateInsurance, "IN-GJ");
		establishment.Configure("IN-GJ", "ESIC-GJ-001", 18, new DateTime(2026, 1, 1),
			new DateTime(2026, 1, 1), "Registration certificate");
		establishment.Activate(Guid.NewGuid(), new DateTime(2026, 1, 1));
		return establishment;
	}

	private static EmployeeStatutoryEnrollment BuildEnrollment(
		EmployeeStatutoryEnrollmentStatus status,
		string? insuranceNumber = null)
	{
		var enrollment = new EmployeeStatutoryEnrollment(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
			StatutorySchemeCodes.EmployeeStateInsurance);
		enrollment.Update(status, StatutoryWageCeilingCategory.Standard, insuranceNumber,
			new DateTime(2026, 4, 1), null, null, null);
		return enrollment;
	}

	private static EmployeePayrollComponent BuildComponent(
		Guid employeeId,
		Guid componentId,
		string name,
		decimal amount,
		bool isEarning,
		PayrollComponentSemanticType semanticType) =>
		new(
			Guid.NewGuid(), employeeId, name, componentId, null, amount,
			isFixed: true, isTemp: false, applicableMonth: null,
			SalaryComponentType.Fixed, SalaryComponentDependency.None,
			isEarning, usesMasterValue: true, semanticType);
}
