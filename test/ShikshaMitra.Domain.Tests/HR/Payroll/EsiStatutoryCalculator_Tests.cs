using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Payroll.Statutory;
using ShikshaMitra.HR.Payroll.Templates;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class EsiStatutoryCalculator_Tests
{
	[Theory]
	[InlineData(20000, 5000, 20000, 150, 650, "WITHIN_WAGE_CEILING")]
	[InlineData(20000, 24000, 22000, 165, 715, "WITHIN_WAGE_CEILING")]
	[InlineData(30000, 5000, 30000, 0, 0, "ABOVE_WAGE_CEILING")]
	public void Generated_overtime_is_classified_but_not_regular_monthly_coverage(
		decimal salary, decimal overtimeAmount, decimal wages, decimal employeeShare, decimal employerShare, string coverage)
	{
		var id = Guid.NewGuid();
		var basic = BuildComponent(id, Guid.NewGuid(), "Basic Salary", salary, true, PayrollComponentSemanticType.Basic);
		var overtime = new EmployeePayrollComponent(Guid.NewGuid(), id, "Overtime Pay", null, null,
			overtimeAmount, true, true, new DateTime(2026, 8, 1), SalaryComponentType.Fixed, SalaryComponentDependency.None, true);
		var deduction = BuildComponent(id, Guid.NewGuid(), "System ESI", 0m, false, PayrollComponentSemanticType.EmployeeStateInsurance);
		var context = new PayrollEsiStatutoryContext(new(2026, 8, 1), BuildRule(), BuildActiveEstablishment(),
			BuildEnrollment(EmployeeStatutoryEnrollmentStatus.PendingReview, "1234567890"),
			new Dictionary<Guid, StatutoryWageTreatment> { [basic.ComponentId!.Value] = StatutoryWageTreatment.Included },
			[], deduction) { SystemOvertimeComponentId = overtime.Id };
		var result = PayrollComputation.Calculate(26, 0, 26m, 26m, [basic, overtime],
			new(null, [], EmployeeStateInsurance: context));
		result.EmployeeStateInsuranceCoverageDecision.ShouldBe(coverage);
		result.EmployeeStateInsuranceWages.ShouldBe(wages);
		result.Deductions.Sum(x => x.Amount).ShouldBe(employeeShare);
		result.EmployerContributionTotal.ShouldBe(employerShare);
		result.NetPay.ShouldBe(salary + overtimeAmount - employeeShare);
		result.StatutoryIssueCodes.ShouldBeEmpty();

		// A user-created row with the same display name must still be classified.
		var custom = PayrollComputation.Calculate(26, 0, 26m, 26m, [basic, overtime],
			new(null, [], EmployeeStateInsurance: context with { SystemOvertimeComponentId = null }));
		custom.StatutoryIssueCodes.ShouldContain(EsiStatutoryCalculator.WageClassificationIssue);
	}

	[Theory]
	[InlineData(4576, 0, 149)]
	[InlineData(4576.01, 35, 149)]
	[InlineData(4577, 35, 149)]
	public void Employee_daily_wage_exemption_preserves_employer_share(decimal wages, decimal employee, decimal employer)
	{
		var result = EsiStatutoryCalculator.Calculate(new(2026, 8, 1), BuildRule(), BuildActiveEstablishment(),
			BuildEnrollment(EmployeeStatutoryEnrollmentStatus.RegistrationPending),
			[new(Guid.NewGuid(), "Basic", wages, StatutoryWageTreatment.Included)]);
		result.EmployeeContribution.ShouldBe(employee);
		result.EmployerContribution.ShouldBe(employer);
	}
	[Theory]
	[InlineData(2026, 3, 2025, 10, 2026, 3)]
	[InlineData(2026, 4, 2026, 4, 2026, 9)]
	[InlineData(2026, 9, 2026, 4, 2026, 9)]
	[InlineData(2026, 10, 2026, 10, 2027, 3)]
	public void Contribution_period_boundaries(int year, int month, int startYear, int startMonth, int endYear, int endMonth)
	{
		var (start, end) = EsiStatutoryCalculator.GetContributionPeriod(new(year, month, 1));
		start.ShouldBe(new DateTime(startYear, startMonth, 1));
		end.ShouldBe(new DateTime(endYear, endMonth, DateTime.DaysInMonth(endYear, endMonth)));
	}
	[Fact]
	public void Monthly_classifications_not_gross_determine_coverage_when_pay_is_prorated()
	{
		var lines = new[]
		{
			new StatutoryWageLine(Guid.NewGuid(), "Basic", 12000m, StatutoryWageTreatment.Included),
			new StatutoryWageLine(Guid.NewGuid(), "HRA", 18000m, StatutoryWageTreatment.SpecifiedExclusion)
		};
		var result = EsiStatutoryCalculator.Calculate(new DateTime(2026, 8, 1), BuildRule(),
			BuildActiveEstablishment(), BuildEnrollment(EmployeeStatutoryEnrollmentStatus.Covered, "SYNTHETIC-TEST"),
			lines.Select(line => line with { Amount = line.Amount / 2 }).ToList(), coverageWageLines: lines);
		result.CoverageDecision.ShouldBe("WITHIN_WAGE_CEILING");
		result.StatutoryWages.ShouldBe(7500m);
		result.EmployeeContribution.ShouldBe(57m);
		result.EmployerContribution.ShouldBe(244m);
	}

	[Theory]
	[InlineData(0, 0, 0)]
	[InlineData(12000, 90, 390)]
	public void Period_continuation_uses_monthly_coverage_but_contributions_use_paid_wages(
		decimal paidWages, decimal employee, decimal employer)
	{
		var enrollment = BuildEnrollment(EmployeeStatutoryEnrollmentStatus.Covered, "SYNTHETIC-TEST");
		enrollment.Update(EmployeeStatutoryEnrollmentStatus.Covered, StatutoryWageCeilingCategory.Standard,
			"SYNTHETIC-TEST", new DateTime(2026, 4, 1), null, new DateTime(2026, 9, 30), null);
		var monthly = new StatutoryWageLine(Guid.NewGuid(), "Basic", 30000m, StatutoryWageTreatment.Included);
		var result = EsiStatutoryCalculator.Calculate(new DateTime(2026, 8, 1), BuildRule(),
			BuildActiveEstablishment(), enrollment, [monthly with { Amount = paidWages }], coverageWageLines: [monthly]);
		result.CoverageDecision.ShouldBe("CONTRIBUTION_PERIOD_CONTINUATION");
		result.EmployeeContribution.ShouldBe(employee);
		result.EmployerContribution.ShouldBe(employer);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(10)]
	[InlineData(20)]
	public void Unpaid_days_do_not_bring_an_above_ceiling_monthly_employee_into_esi(int paidDays)
	{
		var employeeId = Guid.NewGuid();
		var basicId = Guid.NewGuid();
		var basic = BuildComponent(employeeId, basicId, "Basic Salary", 30000m, true, PayrollComponentSemanticType.Basic);
		var esi = BuildComponent(employeeId, Guid.NewGuid(), "System ESI", 0m, false, PayrollComponentSemanticType.EmployeeStateInsurance);
		var context = new PayrollEsiStatutoryContext(new DateTime(2026, 8, 1), BuildRule(), BuildActiveEstablishment(),
			BuildEnrollment(EmployeeStatutoryEnrollmentStatus.NotCovered),
			new Dictionary<Guid, StatutoryWageTreatment> { [basicId] = StatutoryWageTreatment.Included }, [], esi);
		var result = PayrollComputation.Calculate(paidDays, 0, paidDays, 25m, [basic, esi],
			new PayrollStatutoryRuleSet(null, [], EmployeeStateInsurance: context));
		result.EmployeeStateInsuranceCoverageDecision.ShouldBe("ABOVE_WAGE_CEILING");
		result.EmployeeStateInsuranceWages.ShouldBe(30000m * paidDays / 25m);
		result.Deductions.ShouldBeEmpty();
		result.EmployerContributions.ShouldBeEmpty();
		result.StatutoryIssueCodes.ShouldBeEmpty();
	}

	[Theory]
	[InlineData(25, 18000, 1800, 135, 16065, 20385)]
	[InlineData(20, 14400, 1728, 108, 12564, 16596)]
	public void Basic_PF_ESI_should_reconcile_for_full_and_partial_attendance(
		int paidDays, decimal gross, decimal employeePf, decimal employeeEsi, decimal net, decimal employerCost)
	{
		var employeeId = Guid.NewGuid();
		var basicId = Guid.NewGuid();
		var basic = BuildComponent(employeeId, basicId, "Basic Salary", 18000m, true, PayrollComponentSemanticType.Basic);
		var pf = BuildComponent(employeeId, Guid.NewGuid(), "Provident Fund", 1800m, false, PayrollComponentSemanticType.EmployeeProvidentFund);
		var esi = BuildComponent(employeeId, Guid.NewGuid(), "System ESI", 0m, false, PayrollComponentSemanticType.EmployeeStateInsurance);
		var pfRule = new ProvidentFund(Guid.NewGuid(), null, "TEST_PF", "Synthetic PF fixture", 12m, 12m, PfContributionBasis.Basic, 15000m);
		var context = new PayrollEsiStatutoryContext(new DateTime(2026, 8, 1), BuildRule(), BuildActiveEstablishment(),
			BuildEnrollment(EmployeeStatutoryEnrollmentStatus.Covered, "SYNTHETIC-ESI-TEST"),
			new Dictionary<Guid, StatutoryWageTreatment> { [basicId] = StatutoryWageTreatment.Included }, [], esi);
		var result = PayrollComputation.Calculate(paidDays, 0, paidDays, 25m, [basic, pf, esi],
			new PayrollStatutoryRuleSet(pfRule, [], EmployeeStateInsurance: context));
		result.GrossSalary.ShouldBe(gross);
		result.Earnings.Count.ShouldBe(1);
		result.Deductions.Single(x => x.Source.SemanticType == PayrollComponentSemanticType.EmployeeProvidentFund).Amount.ShouldBe(employeePf);
		result.Deductions.Single(x => x.Source.SemanticType == PayrollComponentSemanticType.EmployeeStateInsurance).Amount.ShouldBe(employeeEsi);
		result.NetPay.ShouldBe(net);
		(result.GrossSalary + result.EmployerContributions.Sum(x => x.Amount)).ShouldBe(employerCost);
		result.StatutoryIssueCodes.ShouldBeEmpty();
	}

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
	public void Should_assign_eligible_pwd_employer_share_to_government_without_increasing_employer_cost()
	{
		var enrollment = BuildEnrollment(EmployeeStatutoryEnrollmentStatus.Covered, "ESI-1002");
		enrollment.Update(
			EmployeeStatutoryEnrollmentStatus.Covered,
			StatutoryWageCeilingCategory.PersonWithDisability,
			"ESI-1002",
			new DateTime(2026, 4, 1),
			null,
			null,
			null,
			employerShareReliefEligible: true,
			employerShareReliefFrom: new DateTime(2026, 4, 1),
			employerShareReliefTo: null,
			disabilityEvidenceReference: "PWD-CERT-1002");

		var result = EsiStatutoryCalculator.Calculate(
			new DateTime(2026, 8, 1),
			BuildRule(),
			BuildActiveEstablishment(),
			enrollment,
			[new StatutoryWageLine(Guid.NewGuid(), "Salary", 20_000m, StatutoryWageTreatment.Included)]);

		result.EmployeeContribution.ShouldBe(150m);
		result.EmployerContribution.ShouldBe(0m);
		result.GovernmentContribution.ShouldBe(650m);
		result.IssueCodes.ShouldNotContain(EsiStatutoryCalculator.DisabilityReliefEvidenceIssue);
	}

	[Fact]
	public void Should_apply_employer_override_before_assigning_pwd_share_to_government()
	{
		var employeeId = Guid.NewGuid();
		var enrollment = new EmployeeStatutoryEnrollment(Guid.NewGuid(), Guid.NewGuid(), employeeId,
			StatutorySchemeCodes.EmployeeStateInsurance);
		enrollment.Update(
			EmployeeStatutoryEnrollmentStatus.Covered,
			StatutoryWageCeilingCategory.PersonWithDisability,
			"ESI-1002",
			new DateTime(2026, 4, 1),
			null,
			null,
			null,
			employerShareReliefEligible: true,
			employerShareReliefFrom: new DateTime(2026, 4, 1),
			disabilityEvidenceReference: "PWD-CERT-1002");
		var employerOverride = new StatutoryOverride(
			Guid.NewGuid(), Guid.NewGuid(), employeeId, StatutorySchemeCodes.EmployeeStateInsurance,
			StatutoryOverrideType.EmployerAmount, 700m, "Approved correction", "ORDER-1",
			new DateTime(2026, 8, 1), new DateTime(2026, 8, 31), Guid.NewGuid());

		var result = EsiStatutoryCalculator.Calculate(
			new DateTime(2026, 8, 1),
			BuildRule(),
			BuildActiveEstablishment(),
			enrollment,
			[new StatutoryWageLine(Guid.NewGuid(), "Salary", 20_000m, StatutoryWageTreatment.Included)],
			[employerOverride]);

		result.EmployerContribution.ShouldBe(0m);
		result.GovernmentContribution.ShouldBe(700m);
	}

	[Fact]
	public void Should_reject_pwd_relief_for_a_standard_wage_ceiling_enrollment()
	{
		var enrollment = BuildEnrollment(EmployeeStatutoryEnrollmentStatus.Covered, "ESI-1002");

		Should.Throw<Volo.Abp.BusinessException>(() => enrollment.Update(
			EmployeeStatutoryEnrollmentStatus.Covered,
			StatutoryWageCeilingCategory.Standard,
			"ESI-1002",
			new DateTime(2026, 4, 1),
			null,
			null,
			null,
			employerShareReliefEligible: true,
			employerShareReliefFrom: new DateTime(2026, 4, 1),
			disabilityEvidenceReference: "PWD-CERT-1002"));
	}

	[Fact]
	public void Should_reject_pwd_employer_share_relief_when_evidence_is_missing()
	{
		var enrollment = BuildEnrollment(EmployeeStatutoryEnrollmentStatus.Covered, "ESI-1002");

		Should.Throw<Volo.Abp.BusinessException>(() => enrollment.Update(
			EmployeeStatutoryEnrollmentStatus.Covered,
			StatutoryWageCeilingCategory.PersonWithDisability,
			"ESI-1002",
			new DateTime(2026, 4, 1),
			null,
			null,
			null,
			employerShareReliefEligible: true,
			employerShareReliefFrom: new DateTime(2026, 4, 1)));
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
	public void Off_omits_existing_esi_component_and_all_statutory_presentation_fields()
	{
		var employeeId = Guid.NewGuid();
		var configurationId = Guid.NewGuid();
		var basic = BuildComponent(employeeId, Guid.NewGuid(), "Basic", 10_000m, true, PayrollComponentSemanticType.Basic);
		var esi = BuildComponent(employeeId, Guid.NewGuid(), "ESI", 100m, false, PayrollComponentSemanticType.EmployeeStateInsurance);
		var result = PayrollComputation.Calculate(30, 0, 30m, 30m, [basic, esi],
			new PayrollStatutoryRuleSet(null, [], EsiConfigurationId: configurationId));
		result.Deductions.ShouldBeEmpty();
		result.EmployerContributions.ShouldBeEmpty();
		result.EmployeeStateInsuranceRuleId.ShouldBeNull();
		result.EmployeeStateInsuranceNumber.ShouldBeNull();
		result.EmployeeStateInsuranceWages.ShouldBe(0);
		result.NetPay.ShouldBe(10_000m);
		result.EsiConfigurationId.ShouldBe(configurationId);
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
