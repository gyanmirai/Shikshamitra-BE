using Microsoft.Extensions.Configuration;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Payroll.Statutory;
using ShikshaMitra.Seeders;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ShikshaMitra.HR.Seeders;

public class EsiFullDemoSeedCatalog_Tests
{
	[Fact]
	public void FullDemo_step_should_be_versioned_and_run_only_after_salary_templates()
	{
		EsiFullDemoConfigurationSeederContributor.StepName.ShouldBe("EsiFullDemoConfigurationSeeder");
		EsiFullDemoConfigurationSeederContributor.StepVersion.ShouldBe("EsiFullDemoConfigurationSeeder:v1");
		EsiFullDemoConfigurationSeederContributor.StepOrder.ShouldBe(163);
		EsiFullDemoConfigurationSeederContributor.StepOrder
			.ShouldBeGreaterThan(SalaryTemplateBackfillSeederContributor.StepOrder);
		ShikshaMitraSeederProfile.FullDemoOnly.ShouldBe([ShikshaMitraSeederProfile.FullDemo]);
	}

	[Fact]
	public void Salary_scenarios_should_not_change_FastDemo_or_Minimal_profiles()
	{
		var fullDemo = BuildConfiguration(ShikshaMitraSeederProfile.FullDemo);
		var fastDemo = BuildConfiguration(ShikshaMitraSeederProfile.FastDemo);
		var minimal = BuildConfiguration(ShikshaMitraSeederProfile.Minimal);

		HRSeederContributor.ResolveShouldSeedEsiFullDemoSalaryScenarios(fullDemo).ShouldBeTrue();
		HRSeederContributor.ResolveShouldSeedEsiFullDemoSalaryScenarios(fastDemo).ShouldBeFalse();
		HRSeederContributor.ResolveShouldSeedEsiFullDemoSalaryScenarios(minimal).ShouldBeFalse();
	}

	[Fact]
	public void Active_seed_earnings_should_have_explicit_ESI_treatments()
	{
		var earningSpecs = HRSeederContributor.BuildSeedEarningComponentSpecs();

		earningSpecs.Select(x => x.Name).ShouldBe(
			EsiFullDemoSeedCatalog.ComponentTreatments.Keys,
			ignoreOrder: true);
		EsiFullDemoSeedCatalog.ComponentTreatments["Basic Salary"].ShouldBe(StatutoryWageTreatment.Included);
		EsiFullDemoSeedCatalog.ComponentTreatments["Dearness Allowance (DA)"].ShouldBe(StatutoryWageTreatment.Included);
		EsiFullDemoSeedCatalog.ComponentTreatments["Special Allowance"].ShouldBe(StatutoryWageTreatment.Included);
		EsiFullDemoSeedCatalog.ComponentTreatments["Medical Allowance"].ShouldBe(StatutoryWageTreatment.Included);
		EsiFullDemoSeedCatalog.ComponentTreatments["Performance Bonus"].ShouldBe(StatutoryWageTreatment.Included);
		EsiFullDemoSeedCatalog.ComponentTreatments["Incentive"].ShouldBe(StatutoryWageTreatment.Included);
		EsiFullDemoSeedCatalog.ComponentTreatments["House Rent Allowance (HRA)"]
			.ShouldBe(StatutoryWageTreatment.SpecifiedExclusion);
		EsiFullDemoSeedCatalog.ComponentTreatments["Transport Allowance"]
			.ShouldBe(StatutoryWageTreatment.SpecifiedExclusion);
		earningSpecs.Single(x => x.Name == "Medical Allowance").Description.ShouldBe("Medical reimbursement");
		earningSpecs.Single(x => x.Name == "Performance Bonus").Description.ShouldBe("Variable performance-based bonus");
		earningSpecs.Single(x => x.Name == "Incentive").Description.ShouldBe("One-time incentive payment");
		EsiFullDemoSeedCatalog.ContractDescriptions["Medical Allowance"]
			.ShouldContain("monthly contractual remuneration");
		EsiFullDemoSeedCatalog.ContractDescriptions["Performance Bonus"].ShouldContain("Contractual monthly");
		EsiFullDemoSeedCatalog.ContractDescriptions["Incentive"].ShouldContain("Contractual monthly");
	}

	[Theory]
	[InlineData(EsiFullDemoSeedCatalog.AdmissionsReceptionEmail, 18000, 12800, 96, 416)]
	[InlineData(EsiFullDemoSeedCatalog.AdmissionsCounselorEmail, 20000, 14400, 108, 468)]
	[InlineData(EsiFullDemoSeedCatalog.ClerkOfficeEmail, 30000, 22400, 168, 728)]
	public void Salary_scenarios_should_reconcile_and_calculate_expected_ESI(
		string email,
		int grossSalary,
		int statutoryWages,
		int employeeContribution,
		int employerContribution)
	{
		var salaryScenario = EsiFullDemoSeedCatalog.FindSalaryScenario(email);
		salaryScenario.ShouldNotBeNull();
		var salary = HRSeederContributor.BuildSeededSalaryProfile(
			salaryScenario.GrossSalary,
			seed: 0,
			salaryScenario.SuppressVariablePay);

		salary.GrossSalary.ShouldBe(grossSalary);
		salary.Earnings.Values.Sum().ShouldBe(grossSalary);
		if (salaryScenario.SuppressVariablePay)
		{
			salary.Earnings["Performance Bonus"].ShouldBe(0m);
			salary.Earnings["Incentive"].ShouldBe(0m);
		}

		var result = Calculate(email, salary, new DateTime(2026, 8, 1));
		result.StatutoryWages.ShouldBe(statutoryWages);
		result.EmployeeContribution.ShouldBe(employeeContribution);
		result.EmployerContribution.ShouldBe(employerContribution);
		result.IssueCodes.ShouldBeEmpty();
	}

	[Theory]
	[InlineData(2026, 4, 1, 2026, 4, 1, 2026, 9, 30)]
	[InlineData(2026, 9, 30, 2026, 4, 1, 2026, 9, 30)]
	[InlineData(2026, 10, 1, 2026, 10, 1, 2027, 3, 31)]
	[InlineData(2027, 2, 1, 2026, 10, 1, 2027, 3, 31)]
	public void Contribution_period_should_follow_ESI_half_years(
		int year,
		int month,
		int day,
		int startYear,
		int startMonth,
		int startDay,
		int endYear,
		int endMonth,
		int endDay)
	{
		var period = EsiFullDemoSeedCatalog.ResolveContributionPeriod(new DateTime(year, month, day));

		period.Start.ShouldBe(new DateTime(startYear, startMonth, startDay));
		period.End.ShouldBe(new DateTime(endYear, endMonth, endDay));
	}

	[Fact]
	public void Synthetic_identifiers_should_be_unmistakable_and_custom_establishments_should_not_match()
	{
		EsiFullDemoSeedCatalog.SyntheticRegistrationNumber.ShouldStartWith("DEMO-");
		EsiFullDemoSeedCatalog.SyntheticEvidenceReference.ShouldContain("NOT-FOR-FILING");
		EsiFullDemoSeedCatalog.IsSyntheticEstablishment(
			EsiFullDemoSeedCatalog.SyntheticRegistrationNumber,
			EsiFullDemoSeedCatalog.SyntheticEvidenceReference).ShouldBeTrue();
		EsiFullDemoSeedCatalog.IsSyntheticEstablishment(
			"REAL-ESIC-REGISTRATION",
			"Uploaded registration certificate").ShouldBeFalse();
	}

	private static EsiStatutoryCalculationResult Calculate(
		string email,
		HRSeederContributor.SeededSalaryProfile salary,
		DateTime effectiveOn)
	{
		var rule = new StatutoryRuleVersion(
			Guid.NewGuid(),
			Guid.NewGuid(),
			StatutorySchemeCodes.EmployeeStateInsurance,
			EsiFullDemoSeedCatalog.JurisdictionCode,
			1,
			EsiFullDemoSeedCatalog.RuleEffectiveFrom,
			null,
			10,
			21_000m,
			25_000m,
			0.75m,
			3.25m,
			50m,
			15m,
			StatutoryRoundingPolicy.NextHigherRupee,
			"https://labour.gov.in/",
			new DateTime(2026, 8, 31));
		var establishment = new StatutoryEstablishment(
			Guid.NewGuid(),
			Guid.NewGuid(),
			StatutorySchemeCodes.EmployeeStateInsurance,
			EsiFullDemoSeedCatalog.JurisdictionCode);
		establishment.Configure(
			EsiFullDemoSeedCatalog.JurisdictionCode,
			EsiFullDemoSeedCatalog.SyntheticRegistrationNumber,
			33,
			EsiFullDemoSeedCatalog.RuleEffectiveFrom,
			EsiFullDemoSeedCatalog.RuleEffectiveFrom,
			EsiFullDemoSeedCatalog.SyntheticEvidenceReference);
		var wageLines = salary.Earnings
			.Where(x => x.Value > 0m)
			.Select(x => new StatutoryWageLine(
				Guid.NewGuid(),
				x.Key,
				x.Value,
				EsiFullDemoSeedCatalog.ComponentTreatments[x.Key]))
			.ToList();
		var initial = EsiStatutoryCalculator.Calculate(
			effectiveOn,
			rule,
			establishment,
			null,
			wageLines,
			assumeEstablishmentActive: true);
		var scenario = EsiFullDemoSeedCatalog.ResolveEnrollmentScenario(
			email,
			EmployeeStatusType.Active,
			initial.StatutoryWages,
			rule.StandardWageCeiling,
			effectiveOn);
		var enrollment = new EmployeeStatutoryEnrollment(
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid(),
			StatutorySchemeCodes.EmployeeStateInsurance);
		enrollment.Update(
			scenario.Status,
			StatutoryWageCeilingCategory.Standard,
			scenario.InsuranceNumber,
			scenario.CoveredFrom,
			null,
			scenario.ContributionPeriodEnd,
			scenario.StatusReason);

		return EsiStatutoryCalculator.Calculate(
			effectiveOn,
			rule,
			establishment,
			enrollment,
			wageLines,
			assumeEstablishmentActive: true);
	}

	private static IConfiguration BuildConfiguration(string profile) =>
		new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				[ShikshaMitraSeederProfile.ContextPropertyName] = profile
			})
			.Build();
}
