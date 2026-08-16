using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.Seeders;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore.Tests.HR.Payroll;

public class PayrollStatutorySettingsBackfillSeederContributor_Integration_Tests
	: ShikshaMitraEntityFrameworkCoreTestBase
{
	private readonly PayrollStatutorySettingsBackfillSeederContributor _seeder;
	private readonly IProvidentFundRepository _providentFundRepository;
	private readonly IProfessionalTaxRepository _professionalTaxRepository;
	private readonly ICurrentTenant _currentTenant;
	private readonly IGuidGenerator _guidGenerator;

	public PayrollStatutorySettingsBackfillSeederContributor_Integration_Tests()
	{
		_seeder = GetRequiredService<PayrollStatutorySettingsBackfillSeederContributor>();
		_providentFundRepository = GetRequiredService<IProvidentFundRepository>();
		_professionalTaxRepository = GetRequiredService<IProfessionalTaxRepository>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
		_guidGenerator = GetRequiredService<IGuidGenerator>();
	}

	[Fact]
	public async Task Should_backfill_each_tenant_once_and_preserve_custom_rules()
	{
		var firstTenantId = _guidGenerator.Create();
		var secondTenantId = _guidGenerator.Create();

		await WithTenantAsync(firstTenantId, async () =>
		{
			await _providentFundRepository.InsertAsync(
				new ProvidentFund(
					_guidGenerator.Create(),
					firstTenantId,
					"CUSTOM-5",
					"Custom Five Percent PF",
					5m,
					5m,
					PfContributionBasis.Basic,
					wageCeiling: null,
					effectiveFrom: new DateTime(2025, 4, 1)),
				autoSave: true);

			await _professionalTaxRepository.InsertAsync(
				new ProfessionalTax(
					_guidGenerator.Create(),
					firstTenantId,
					"IN-GJ",
					new DateTime(2025, 4, 1),
					null,
					0m,
					10_000m,
					100m),
				autoSave: true);
		});

		await SeedTenantAsync(firstTenantId);
		await SeedTenantAsync(firstTenantId);
		await SeedTenantAsync(secondTenantId);

		await WithTenantAsync(firstTenantId, async () =>
		{
			var providentFunds = await _providentFundRepository.GetListAsync();
			var professionalTaxes = await _professionalTaxRepository.GetListAsync();

			providentFunds.Count.ShouldBe(2);
			providentFunds.ShouldAllBe(rule => rule.TenantId == firstTenantId);
			providentFunds.ShouldContain(rule => rule.Code == "CUSTOM-5" && rule.EmployeePercentage == 5m);
			AssertStandardProvidentFund(providentFunds.Single(rule => rule.Code == "STD-PF"));

			professionalTaxes.Count.ShouldBe(3);
			professionalTaxes.ShouldAllBe(rule => rule.TenantId == firstTenantId);
			professionalTaxes.ShouldContain(rule =>
				rule.EffectiveFrom == new DateTime(2025, 4, 1) && rule.TaxAmount == 100m);
			AssertGujaratProfessionalTaxSchedule(professionalTaxes);
		});

		await WithTenantAsync(secondTenantId, async () =>
		{
			var providentFunds = await _providentFundRepository.GetListAsync();
			var professionalTaxes = await _professionalTaxRepository.GetListAsync();

			providentFunds.Count.ShouldBe(1);
			providentFunds.ShouldAllBe(rule => rule.TenantId == secondTenantId);
			AssertStandardProvidentFund(providentFunds.Single());

			professionalTaxes.Count.ShouldBe(2);
			professionalTaxes.ShouldAllBe(rule => rule.TenantId == secondTenantId);
			AssertGujaratProfessionalTaxSchedule(professionalTaxes);
		});
	}

	private async Task SeedTenantAsync(Guid tenantId)
	{
		await WithTenantAsync(tenantId, () =>
			_seeder.SeedAsync(new DataSeedContext(tenantId)));
	}

	private async Task WithTenantAsync(Guid tenantId, Func<Task> action)
	{
		using (_currentTenant.Change(tenantId))
		{
			await WithUnitOfWorkAsync(action);
		}
	}

	private static void AssertStandardProvidentFund(ProvidentFund rule)
	{
		rule.Name.ShouldBe("Standard Provident Fund");
		rule.EmployeePercentage.ShouldBe(12m);
		rule.EmployerPercentage.ShouldBe(12m);
		rule.ContributionBasis.ShouldBe(PfContributionBasis.Basic);
		rule.WageCeiling.ShouldBe(15_000m);
		rule.EffectiveFrom.ShouldBe(new DateTime(2022, 4, 1));
		rule.IsActive.ShouldBeTrue();
	}

	private static void AssertGujaratProfessionalTaxSchedule(
		System.Collections.Generic.IReadOnlyCollection<ProfessionalTax> rules)
	{
		rules.ShouldContain(rule =>
			rule.EffectiveFrom == new DateTime(2022, 4, 1) &&
			rule.FromSalary == 0m &&
			rule.ToSalary == 12_000m &&
			rule.TaxAmount == 0m);
		rules.ShouldContain(rule =>
			rule.EffectiveFrom == new DateTime(2022, 4, 1) &&
			rule.FromSalary == 12_000.01m &&
			rule.ToSalary == null &&
			rule.TaxAmount == 200m);
	}
}
