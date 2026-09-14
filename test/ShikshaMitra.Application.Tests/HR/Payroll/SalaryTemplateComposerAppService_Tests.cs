using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Payroll.Templates;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class SalaryTemplateComposerAppService_Tests
	: ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly ISalaryTemplateAppService _appService;
	private readonly IRepository<EarningComponent, Guid> _earningRepository;
	private readonly IRepository<DeductionComponent, Guid> _deductionRepository;
	private readonly ICurrentTenant _currentTenant;

	public SalaryTemplateComposerAppService_Tests()
	{
		_appService = GetRequiredService<ISalaryTemplateAppService>();
		_earningRepository = GetRequiredService<IRepository<EarningComponent, Guid>>();
		_deductionRepository = GetRequiredService<IRepository<DeductionComponent, Guid>>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
	}

	[Fact]
	public async Task Should_keep_blank_draft_unpublishable_and_publish_basic_100_without_balancer()
	{
		var suffix = Guid.NewGuid().ToString("N")[..8];
		var basic = await InsertEarningAsync($"Composer Basic {suffix}");
		var template = await _appService.CreateAsync(new CreateSalaryTemplateDto
		{
			Name = $"Composer Template {suffix}",
			Description = "Flexible salary template"
		});

		template.Status.ShouldBe("DRAFT");
		template.Latest.Rules.ShouldBeEmpty();
		(await Should.ThrowAsync<BusinessException>(() =>
			_appService.PublishAsync(template.Id, template.Latest.Id))).Code.ShouldBe("HR:Payroll:SalaryTemplateRulesRequired");

		var updated = await _appService.UpdateDraftAsync(
			template.Id,
			template.Latest.Id,
			new UpdateSalaryTemplateDraftDto
			{
				Name = template.Name,
				Description = template.Description,
				Rules =
				[
					new SalaryTemplateRuleDto
					{
						Code = "CLIENT_CONTROLLED_CODE",
						ComponentId = basic.Id,
						LineKind = "EARNING",
						SemanticType = "BASIC",
						FormulaType = "PERCENTAGE_OF_GROSS",
						Value = 100m,
						DisplayOrder = 0
					}
				]
			});

		updated.Latest.Rules.Single().Code.ShouldNotBe("CLIENT_CONTROLLED_CODE");
		var published = await _appService.PublishAsync(template.Id, template.Latest.Id);
		published.Status.ShouldBe("PUBLISHED");
		published.Latest.Rules.Count.ShouldBe(1);
	}

	[Fact]
	public async Task Should_return_active_tenant_component_catalog_and_resolve_basis_component()
	{
		var suffix = Guid.NewGuid().ToString("N")[..8];
		var basic = await InsertEarningAsync($"Catalog Basic {suffix}");
		var allowance = await InsertEarningAsync($"Catalog Allowance {suffix}");
		var deduction = await InsertDeductionAsync($"Catalog Deduction {suffix}");
		var catalog = await _appService.GetComponentCatalogAsync();

		catalog.Items.ShouldContain(x =>
			x.ComponentId == basic.Id && x.Name == basic.Name && x.LineKind == "EARNING");
		catalog.Items.ShouldContain(x =>
			x.ComponentId == deduction.Id && x.Name == deduction.Name && x.LineKind == "DEDUCTION");

		var template = await _appService.CreateAsync(new CreateSalaryTemplateDto
		{
			Name = $"Basis Template {suffix}"
		});
		var updated = await _appService.UpdateDraftAsync(
			template.Id,
			template.Latest.Id,
			new UpdateSalaryTemplateDraftDto
			{
				Name = template.Name,
				Rules =
				[
					new SalaryTemplateRuleDto
					{
						ComponentId = basic.Id,
						LineKind = "EARNING",
						SemanticType = "BASIC",
						FormulaType = "PERCENTAGE_OF_GROSS",
						Value = 60m,
						DisplayOrder = 0
					},
					new SalaryTemplateRuleDto
					{
						ComponentId = allowance.Id,
						LineKind = "EARNING",
						SemanticType = "ORDINARY",
						FormulaType = "PERCENTAGE_OF_COMPONENT",
						Value = 10m,
						BasisComponentId = basic.Id,
						DisplayOrder = 1
					}
				]
			});

		updated.Latest.Rules.Single(x => x.ComponentId == allowance.Id)
			.BasisComponentId.ShouldBe(basic.Id);
	}

	[Fact]
	public async Task Component_catalog_should_be_tenant_isolated()
	{
		var firstTenantId = Guid.NewGuid();
		var secondTenantId = Guid.NewGuid();
		var firstName = $"First Tenant Earning {Guid.NewGuid():N}";
		var secondName = $"Second Tenant Earning {Guid.NewGuid():N}";
		SalaryTemplateDto firstTemplate = null!;
		EarningComponent secondTenantEarning = null!;

		using (_currentTenant.Change(firstTenantId))
		{
			await WithUnitOfWorkAsync(() => InsertEarningAsync(firstName));
			firstTemplate = await _appService.CreateAsync(new CreateSalaryTemplateDto
			{
				Name = $"First Tenant Template {Guid.NewGuid():N}"
			});
		}

		using (_currentTenant.Change(secondTenantId))
		{
			secondTenantEarning = await WithUnitOfWorkAsync(() => InsertEarningAsync(secondName));
		}

		using (_currentTenant.Change(firstTenantId))
		{
			var firstCatalog = await _appService.GetComponentCatalogAsync();
			firstCatalog.Items.ShouldContain(x => x.Name == firstName);
			firstCatalog.Items.ShouldNotContain(x => x.Name == secondName);
			var exception = await Should.ThrowAsync<BusinessException>(() =>
				_appService.UpdateDraftAsync(
					firstTemplate.Id,
					firstTemplate.Latest.Id,
					new UpdateSalaryTemplateDraftDto
					{
						Name = firstTemplate.Name,
						Rules =
						[
							new SalaryTemplateRuleDto
							{
								ComponentId = secondTenantEarning.Id,
								LineKind = "EARNING",
								SemanticType = "BASIC",
								FormulaType = "PERCENTAGE_OF_GROSS",
								Value = 60m,
								DisplayOrder = 0
							}
						]
					}));
			exception.Code.ShouldBe("HR:Payroll:SalaryTemplateComponentMissing");
		}

		using (_currentTenant.Change(secondTenantId))
		{
			var secondCatalog = await _appService.GetComponentCatalogAsync();
			secondCatalog.Items.ShouldContain(x => x.Name == secondName);
			secondCatalog.Items.ShouldNotContain(x => x.Name == firstName);
		}
	}

	private async Task<EarningComponent> InsertEarningAsync(string name)
	{
		var component = new EarningComponent(
			Guid.NewGuid(),
			_currentTenant.Id,
			name,
			null,
			EarningComponentType.Fixed,
			CalculationType.Amount,
			0m,
			null,
			null,
			ComponentDependency.None,
			true);
		return await _earningRepository.InsertAsync(component, autoSave: true);
	}

	private async Task<DeductionComponent> InsertDeductionAsync(string name)
	{
		var component = new DeductionComponent(
			Guid.NewGuid(),
			_currentTenant.Id,
			name,
			null,
			DeductionComponentType.Fixed,
			CalculationType.Amount,
			0m,
			null,
			null,
			ComponentDependency.None,
			true);
		return await _deductionRepository.InsertAsync(component, autoSave: true);
	}
}
