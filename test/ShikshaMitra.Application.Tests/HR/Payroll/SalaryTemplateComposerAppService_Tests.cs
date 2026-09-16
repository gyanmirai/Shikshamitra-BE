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
	public async Task Should_create_an_active_default_and_replace_it_with_an_immutable_new_version()
	{
		var suffix = Guid.NewGuid().ToString("N")[..8];
		var basic = await InsertEarningAsync($"Composer Basic {suffix}");
		var allowance = await InsertEarningAsync($"Composer Allowance {suffix}");
		var created = await _appService.CreateAsync(Save(
			$"Composer Template {suffix}",
			[new SalaryTemplateRuleDto
			{
				Code = "CLIENT_CONTROLLED_CODE",
				ComponentId = basic.Id,
				LineKind = "EARNING",
				SemanticType = "BASIC",
				FormulaType = "PERCENTAGE_OF_GROSS",
				Value = 100m,
				DisplayOrder = 0
			}]));

		created.Status.ShouldBe("ACTIVE");
		created.IsDefault.ShouldBeTrue();
		created.ActiveVersion.ShouldBe(1);
		created.EditableVersion.Rules.Single().Code.ShouldNotBe("CLIENT_CONTROLLED_CODE");

		var update = Save(created.Name,
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
				SemanticType = "BALANCING_EARNING",
				FormulaType = "REMAINDER_OF_GROSS",
				DisplayOrder = 1
			}
		]);
		update.ExpectedActiveVersionId = created.EditableVersion.Id;
		update.ConcurrencyStamp = created.ConcurrencyStamp;

		var updated = await _appService.UpdateAsync(created.Id, update);
		updated.ActiveVersion.ShouldBe(2);
		updated.VersionCount.ShouldBe(2);
		var history = await _appService.GetVersionsAsync(created.Id);
		history.Items.Single(x => x.VersionNumber == 1).Status.ShouldBe("REPLACED");
		history.Items.Single(x => x.VersionNumber == 1).Rules.Single().Value.ShouldBe(100m);
		history.Items.Single(x => x.VersionNumber == 2).Status.ShouldBe("ACTIVE");

		var stale = Save(created.Name, update.Rules.ToArray());
		stale.ExpectedActiveVersionId = created.EditableVersion.Id;
		stale.ConcurrencyStamp = created.ConcurrencyStamp;
		(await Should.ThrowAsync<BusinessException>(() => _appService.UpdateAsync(created.Id, stale))).Code
			.ShouldBe("HR:Payroll:SalaryTemplateSaveConflict");
	}

	[Fact]
	public async Task Should_return_active_tenant_component_catalog_and_resolve_basis_component()
	{
		var suffix = Guid.NewGuid().ToString("N")[..8];
		var basic = await InsertEarningAsync($"Catalog Basic {suffix}");
		var allowance = await InsertEarningAsync($"Catalog Allowance {suffix}");
		var deduction = await InsertDeductionAsync($"Catalog Deduction {suffix}");
		var catalog = await _appService.GetComponentCatalogAsync();

		catalog.Items.ShouldContain(x => x.ComponentId == basic.Id && x.LineKind == "EARNING");
		catalog.Items.ShouldContain(x => x.ComponentId == deduction.Id && x.LineKind == "DEDUCTION");
		var template = await _appService.CreateAsync(Save($"Basis Template {suffix}",
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
		]));

		template.EditableVersion.Rules.Single(x => x.ComponentId == allowance.Id)
			.BasisComponentId.ShouldBe(basic.Id);
	}

	[Fact]
	public async Task Component_catalog_and_saves_should_be_tenant_isolated()
	{
		var firstTenantId = Guid.NewGuid();
		var secondTenantId = Guid.NewGuid();
		SalaryTemplateDto firstTemplate = null!;
		EarningComponent secondTenantEarning = null!;

		using (_currentTenant.Change(firstTenantId))
		{
			var firstEarning = await WithUnitOfWorkAsync(() => InsertEarningAsync("First Tenant Basic"));
			firstTemplate = await _appService.CreateAsync(Save("First Tenant Template",
			[
				new SalaryTemplateRuleDto
				{
					ComponentId = firstEarning.Id,
					LineKind = "EARNING",
					SemanticType = "BASIC",
					FormulaType = "PERCENTAGE_OF_GROSS",
					Value = 100m
				}
			]));
		}

		using (_currentTenant.Change(secondTenantId))
		{
			secondTenantEarning = await WithUnitOfWorkAsync(() => InsertEarningAsync("Second Tenant Basic"));
		}

		using (_currentTenant.Change(firstTenantId))
		{
			var update = Save(firstTemplate.Name,
			[
				new SalaryTemplateRuleDto
				{
					ComponentId = secondTenantEarning.Id,
					LineKind = "EARNING",
					SemanticType = "BASIC",
					FormulaType = "PERCENTAGE_OF_GROSS",
					Value = 100m
				}
			]);
			update.ExpectedActiveVersionId = firstTemplate.EditableVersion.Id;
			update.ConcurrencyStamp = firstTemplate.ConcurrencyStamp;
			(await Should.ThrowAsync<BusinessException>(() =>
				_appService.UpdateAsync(firstTemplate.Id, update))).Code
				.ShouldBe("HR:Payroll:SalaryTemplateComponentMissing");
		}
	}

	[Fact]
	public async Task Archiving_the_default_should_require_a_replacement_and_keep_history_visible()
	{
		var suffix = Guid.NewGuid().ToString("N")[..8];
		var basic = await InsertEarningAsync($"Archive Basic {suffix}");
		var rule = new SalaryTemplateRuleDto
		{
			ComponentId = basic.Id,
			LineKind = "EARNING",
			SemanticType = "BASIC",
			FormulaType = "PERCENTAGE_OF_GROSS",
			Value = 100m
		};
		var first = await _appService.CreateAsync(Save($"Archive First {suffix}", [rule]));
		var second = await _appService.CreateAsync(Save($"Archive Second {suffix}", [rule]));

		(await Should.ThrowAsync<BusinessException>(() =>
			_appService.ArchiveAsync(first.Id, new ArchiveSalaryTemplateDto()))).Code
			.ShouldBe("HR:Payroll:SalaryTemplateReplacementDefaultRequired");

		await _appService.ArchiveAsync(first.Id, new ArchiveSalaryTemplateDto
		{
			ReplacementDefaultTemplateId = second.Id
		});

		var all = await _appService.GetListAsync();
		all.Items.Single(x => x.Id == first.Id).Status.ShouldBe("ARCHIVED");
		all.Items.Single(x => x.Id == second.Id).IsDefault.ShouldBeTrue();
		var active = await _appService.GetActiveListAsync();
		active.Items.ShouldNotContain(x => x.Id == first.Id);
	}

	private static SaveSalaryTemplateDto Save(string name, SalaryTemplateRuleDto[] rules) => new()
	{
		Name = name,
		Description = "Reusable salary setup",
		DefaultSalaryBasis = "EMPLOYER_COST",
		DefaultProtectionPolicy = "PROTECT_EMPLOYER_COST",
		Rules = rules.ToList(),
		IdempotencyKey = Guid.NewGuid().ToString("N")
	};

	private async Task<EarningComponent> InsertEarningAsync(string name)
	{
		var component = new EarningComponent(
			Guid.NewGuid(), _currentTenant.Id, name, null, EarningComponentType.Fixed,
			CalculationType.Amount, 0m, null, null, ComponentDependency.None, true);
		return await _earningRepository.InsertAsync(component, autoSave: true);
	}

	private async Task<DeductionComponent> InsertDeductionAsync(string name)
	{
		var component = new DeductionComponent(
			Guid.NewGuid(), _currentTenant.Id, name, null, DeductionComponentType.Fixed,
			CalculationType.Amount, 0m, null, null, ComponentDependency.None, true);
		return await _deductionRepository.InsertAsync(component, autoSave: true);
	}
}
