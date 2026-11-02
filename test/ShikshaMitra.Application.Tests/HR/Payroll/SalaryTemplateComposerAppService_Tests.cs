using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Payroll.Templates;
using ShikshaMitra.HR.Payroll.Statutory;
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
	public async Task Bug6539_partial_classification_identifies_HRA_and_does_not_unlock_October_or_relax_CTC_reconciliation()
	{
		using (_currentTenant.Change(Guid.NewGuid()))
		{
			var tenant = _currentTenant.Id;
			var basic = await WithUnitOfWorkAsync(() => InsertEarningAsync("Synthetic Basic"));
			var hra = await WithUnitOfWorkAsync(() => InsertEarningAsync("Synthetic HRA"));
			var policy = new EsiPolicy(Guid.NewGuid(), tenant, "Synthetic ESIC", "SYNTHETIC", true);
			await WithUnitOfWorkAsync(async () =>
			{
				await GetRequiredService<IRepository<EsiPolicy, Guid>>().InsertAsync(policy);
				await GetRequiredService<IRepository<StatutoryRuleVersion, Guid>>().InsertAsync(new(
					Guid.NewGuid(), tenant, "ESI", "IN-GJ", 1, new(2026, 1, 1), null, 10, 21000m, 25000m,
					0.75m, 3.25m, 50m, 15m, StatutoryRoundingPolicy.NextHigherRupee,
					"https://example.invalid/synthetic", new(2026, 1, 1), esiPolicyId: policy.Id, calculationSettings: new()));
				await GetRequiredService<EsiPayrollConfigurationResolver>().EnsureDefaultAsync();
				await GetRequiredService<IRepository<EsiPayrollConfiguration, Guid>>().InsertAsync(new(
					Guid.NewGuid(), tenant, new(2026, 11, 1), true, null, null, "synthetic-november-complete",
					treatments: new System.Collections.Generic.Dictionary<Guid, StatutoryWageTreatment> { [basic.Id] = StatutoryWageTreatment.Included, [hra.Id] = StatutoryWageTreatment.Included }));
			});
			var input = new PreviewSalaryTemplateRequestDto
			{
				SalaryBasis = "GROSS", MonthlyAmount = 10000m, EffectiveMonth = new(2026, 10, 1),
				EsiManagementMode = "IN_PLATFORM", EsiPolicyId = policy.Id,
				Rules = [new() { ComponentId = basic.Id, LineKind = "EARNING", SemanticType = "BASIC", FormulaType = "FIXED_AMOUNT", Value = 6000m },
					new() { ComponentId = hra.Id, LineKind = "EARNING", SemanticType = "ORDINARY", FormulaType = "FIXED_AMOUNT", Value = 4000m, DisplayOrder = 1 }]
			};
			var both = await Should.ThrowAsync<BusinessException>(() => _appService.PreviewAsync(input));
			both.Code.ShouldBe("HR:Payroll:SalaryTemplateSampleBlocked");
			using (var data = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(both.Data["unclassifiedEarnings"])))
				data.RootElement.GetArrayLength().ShouldBe(2);
			await ClassifyAsync([basic.Id]);
			for (var attempt = 0; attempt < 2; attempt++)
			{
				var partial = await Should.ThrowAsync<BusinessException>(() => _appService.PreviewAsync(input));
				partial.Code.ShouldBe("HR:Payroll:SalaryTemplateSampleBlocked");
				partial.Data["effectiveMonth"].ShouldBe("2026-10");
				using var data = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(partial.Data["unclassifiedEarnings"]));
				var earning = data.RootElement.EnumerateArray().ShouldHaveSingleItem();
				earning.GetProperty("componentId").GetGuid().ShouldBe(hra.Id);
				earning.GetProperty("name").GetString().ShouldBe("Synthetic HRA");
				earning.EnumerateObject().Count().ShouldBe(2); // No salary amounts or raw diagnostic text.
			}
			input.EffectiveMonth = new(2026, 11, 1);
			(await _appService.PreviewAsync(input)).Calculation.EmployeeStateInsurance.CanCalculate.ShouldBeTrue();
			input.EffectiveMonth = new(2026, 10, 1);
			await ClassifyAsync([hra.Id]);
			var complete = await _appService.PreviewAsync(input);
			complete.Calculation.EmployeeStateInsurance.UnclassifiedEarnings.ShouldBeEmpty();
			complete.Calculation.GrossSalary.ShouldBe(10000m);
			complete.Calculation.EmployeeStateInsurance.EmployeeContribution.ShouldBe(75m);
			complete.Calculation.EmployerCost.ShouldBe(10325m);
			input.SalaryBasis = "EMPLOYER_COST";
			input.MonthlyAmount = 15000m;
			(await Should.ThrowAsync<BusinessException>(() => _appService.PreviewAsync(input))).Code.ShouldBe(HRErrorCodes.CompensationTargetNotSolvable);
			(await _appService.GetListAsync()).Items.ShouldBeEmpty();

			async Task ClassifyAsync(Guid[] ids) => await WithUnitOfWorkAsync(async () =>
			{
				var current = (await GetRequiredService<EsiPayrollConfigurationResolver>().ResolveAsync(new(2026, 10, 1)))!;
				var treatments = current.GetTreatments();
				foreach (var id in ids) treatments[id] = StatutoryWageTreatment.Included;
				if (current.EffectiveMonth == new DateTime(2026, 10, 1))
				{
					current.Cancel("Synthetic same-month correction");
					await GetRequiredService<IRepository<EsiPayrollConfiguration, Guid>>().UpdateAsync(current, autoSave: true);
				}
				await GetRequiredService<IRepository<EsiPayrollConfiguration, Guid>>().InsertAsync(new(
					Guid.NewGuid(), tenant, new(2026, 10, 1), true, null, null, Guid.NewGuid().ToString(), treatments: treatments));
			});
		}
	}

	[Fact]
	public async Task Bug6534_November_treatment_does_not_satisfy_October_preview_and_same_month_correction_allows_retry()
	{
		using (_currentTenant.Change(Guid.NewGuid()))
		{
			var tenant = _currentTenant.Id;
			var basic = await WithUnitOfWorkAsync(() => InsertEarningAsync("Synthetic custom earning"));
			var policy = new EsiPolicy(Guid.NewGuid(), tenant, "Synthetic ESIC", "SYNTHETIC", true);
			await WithUnitOfWorkAsync(async () =>
			{
				await GetRequiredService<IRepository<EsiPolicy, Guid>>().InsertAsync(policy);
				await GetRequiredService<IRepository<StatutoryRuleVersion, Guid>>().InsertAsync(new(
					Guid.NewGuid(), tenant, "ESI", "IN-GJ", 1, new(2026, 1, 1), null, 10, 21000m, 25000m,
					0.75m, 3.25m, 50m, 15m, StatutoryRoundingPolicy.NextHigherRupee,
					"https://example.invalid/synthetic", new(2026, 1, 1), esiPolicyId: policy.Id,
					calculationSettings: new()));
				await GetRequiredService<EsiPayrollConfigurationResolver>().EnsureDefaultAsync();
				await GetRequiredService<IRepository<EsiPayrollConfiguration, Guid>>().InsertAsync(new(
					Guid.NewGuid(), tenant, new(2026, 11, 1), true, null, null, "synthetic-november",
					treatments: new System.Collections.Generic.Dictionary<Guid, StatutoryWageTreatment> { [basic.Id] = StatutoryWageTreatment.Included }));
			});
			var input = new PreviewSalaryTemplateRequestDto
			{
				SalaryBasis = "GROSS", MonthlyAmount = 10000m, EffectiveMonth = new(2026, 10, 1),
				EsiManagementMode = "IN_PLATFORM", EsiPolicyId = policy.Id,
				Rules = [new() { ComponentId = basic.Id, LineKind = "EARNING", SemanticType = "BASIC", FormulaType = "PERCENTAGE_OF_GROSS", Value = 100m }]
			};
			for (var attempt = 0; attempt < 2; attempt++)
			{
				var blocked = await Should.ThrowAsync<BusinessException>(() => _appService.PreviewAsync(input));
				blocked.Code.ShouldBe("HR:Payroll:SalaryTemplateSampleBlocked");
				blocked.Data["effectiveMonth"].ShouldBe("2026-10");
				((System.Collections.Generic.IEnumerable<string>)blocked.Data["issues"]!).ShouldContain("ESI_WAGE_CLASSIFICATION_REQUIRED");
				blocked.Data["message"].ToString()!.ShouldContain("ESIC treatment is missing for the preview month");
			}
			input.EffectiveMonth = new(2026, 11, 1);
			var november = await _appService.PreviewAsync(input);
			november.Calculation.EmployeeStateInsurance.CanCalculate.ShouldBeTrue();
			november.Calculation.EmployeeStateInsurance.EmployeeContribution.ShouldBe(75m);
			november.Calculation.EmployeeStateInsurance.EmployerContribution.ShouldBe(325m);
			await WithUnitOfWorkAsync(async () =>
				await GetRequiredService<IRepository<EsiPayrollConfiguration, Guid>>().InsertAsync(new(
					Guid.NewGuid(), tenant, new(2026, 10, 1), true, null, null, "synthetic-october",
					treatments: new System.Collections.Generic.Dictionary<Guid, StatutoryWageTreatment> { [basic.Id] = StatutoryWageTreatment.Included })));
			input.EffectiveMonth = new(2026, 10, 1);
			var corrected = await _appService.PreviewAsync(input);
			corrected.PreviewToken.ShouldNotBeNullOrWhiteSpace();
			corrected.Calculation.GrossSalary.ShouldBe(10000m);
			corrected.Calculation.EmployeeStateInsurance.EmployeeContribution.ShouldBe(75m);
			corrected.Calculation.EmployeeStateInsurance.EmployerContribution.ShouldBe(325m);
			(await _appService.GetListAsync()).Items.ShouldBeEmpty();
		}
	}

	[Fact]
	public async Task Preview_does_not_persist_and_save_requires_the_current_sample()
	{
		var basic = await InsertEarningAsync($"Preview Basic {Guid.NewGuid():N}");
		var input = Save($"Preview {Guid.NewGuid():N}", [new SalaryTemplateRuleDto
		{
			ComponentId = basic.Id, LineKind = "EARNING", SemanticType = "BASIC",
			FormulaType = "PERCENTAGE_OF_GROSS", Value = 100m
		}]);
		var before = (await _appService.GetListAsync()).Items.Count;
		await AttachPreviewAsync(input);
		(await _appService.GetListAsync()).Items.Count.ShouldBe(before);
		var validToken = input.PreviewToken;
		input.PreviewToken = "";
		(await Should.ThrowAsync<BusinessException>(() => _appService.CreateAsync(input))).Code
			.ShouldBe("HR:Payroll:SalaryTemplatePreviewRequired");
		input.PreviewToken = validToken;
		input.Rules[0].Value = 60m;
		(await Should.ThrowAsync<BusinessException>(() => _appService.CreateAsync(input))).Code
			.ShouldBe("HR:Payroll:SalaryTemplatePreviewRequired");
		(await Should.ThrowAsync<BusinessException>(() => AttachPreviewAsync(input))).Code
			.ShouldBe("HR:Payroll:CompensationTargetNotSolvable");
		input.Rules[0].Value = 100m;
		input.Name += " renamed";
		var saved = await _appService.CreateAsync(input);
		var replay = await _appService.CreateAsync(input);
		replay.Id.ShouldBe(saved.Id);
		replay.VersionCount.ShouldBe(1);
	}

	[Theory]
	[InlineData("Basic Salary")]
	[InlineData("Teaching pay")]
	public async Task Fresh_tenant_requires_explicit_Basic_purpose_and_preserves_fixed_salary(string earningName)
	{
		using (_currentTenant.Change(Guid.NewGuid()))
		{
			var earning = await InsertEarningAsync(earningName);
			var deduction = await InsertDeductionAsync("Medical Emergency");
			var input = Save("New school salary", [new SalaryTemplateRuleDto
			{
				ComponentId = earning.Id, LineKind = "EARNING", SemanticType = "ORDINARY",
				FormulaType = "FIXED_AMOUNT", Value = 50000m, DisplayOrder = 0
			}, new SalaryTemplateRuleDto
			{
				ComponentId = deduction.Id, LineKind = "DEDUCTION", SemanticType = "ORDINARY",
				FormulaType = "FIXED_AMOUNT", Value = 1000m, DisplayOrder = 1
			}]);
			(await _appService.GetListAsync()).Items.ShouldBeEmpty();
			(await Should.ThrowAsync<BusinessException>(() => AttachPreviewAsync(input, sampleMonthlyAmount: 50000m)))
				.Code.ShouldBe("HR:Payroll:SalaryTemplateRequiresOneBasic");
			(await _appService.GetListAsync()).Items.ShouldBeEmpty();

			// Payroll purpose is explicit metadata on the template rule, independent of its display name.
			input.Rules[0].SemanticType = "BASIC";
			var preview = await AttachPreviewAsync(input, sampleMonthlyAmount: 50000m);
			preview.Calculation.BasicSalary.ShouldBe(50000m);
			preview.Calculation.GrossSalary.ShouldBe(50000m);
			preview.Calculation.TotalDeductions.ShouldBe(1000m);
			preview.Calculation.NetSalary.ShouldBe(49000m);
			var saved = await _appService.CreateAsync(input);
			saved.Status.ShouldBe("ACTIVE");
			var reloaded = await _appService.GetAsync(saved.Id);
			var basic = reloaded.EditableVersion.Rules.Single(rule => rule.ComponentId == earning.Id);
			basic.SemanticType.ShouldBe("BASIC");
			basic.FormulaType.ShouldBe("FIXED_AMOUNT");
			basic.Value.ShouldBe(50000m);
		}
	}

	[Fact]
	public async Task Should_create_an_active_default_and_replace_it_with_an_immutable_new_version()
	{
		var suffix = Guid.NewGuid().ToString("N")[..8];
		var basic = await InsertEarningAsync($"Composer Basic {suffix}");
		var allowance = await InsertEarningAsync($"Composer Allowance {suffix}");
		var created = await CreatePreviewedAsync(Save(
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

		var updated = await UpdatePreviewedAsync(created.Id, update);
		updated.ActiveVersion.ShouldBe(2);
		updated.VersionCount.ShouldBe(2);
		var history = await _appService.GetVersionsAsync(created.Id);
		history.Items.Single(x => x.VersionNumber == 1).Status.ShouldBe("REPLACED");
		history.Items.Single(x => x.VersionNumber == 1).Rules.Single().Value.ShouldBe(100m);
		history.Items.Single(x => x.VersionNumber == 2).Status.ShouldBe("ACTIVE");

		var stale = Save(created.Name, update.Rules.ToArray());
		stale.ExpectedActiveVersionId = created.EditableVersion.Id;
		stale.ConcurrencyStamp = created.ConcurrencyStamp;
		(await Should.ThrowAsync<BusinessException>(() => UpdatePreviewedAsync(created.Id, stale))).Code
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
		var template = await CreatePreviewedAsync(Save($"Basis Template {suffix}",
		[
			new SalaryTemplateRuleDto
			{
				ComponentId = basic.Id,
				LineKind = "EARNING",
				SemanticType = "BASIC",
				FormulaType = "PERCENTAGE_OF_GROSS",
				Value = 50m,
				DisplayOrder = 0
			},
			new SalaryTemplateRuleDto
			{
				ComponentId = allowance.Id,
				LineKind = "EARNING",
				SemanticType = "ORDINARY",
				FormulaType = "PERCENTAGE_OF_COMPONENT",
				Value = 100m,
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
			firstTemplate = await CreatePreviewedAsync(Save("First Tenant Template",
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
				UpdatePreviewedAsync(firstTemplate.Id, update))).Code
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
		var first = await CreatePreviewedAsync(Save($"Archive First {suffix}", [rule]));
		var second = await CreatePreviewedAsync(Save($"Archive Second {suffix}", [rule]));

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

	private async Task<SalaryTemplateDto> CreatePreviewedAsync(SaveSalaryTemplateDto input)
	{
		await AttachPreviewAsync(input);
		return await _appService.CreateAsync(input);
	}

	private async Task<SalaryTemplateDto> UpdatePreviewedAsync(Guid id, SaveSalaryTemplateDto input)
	{
		await AttachPreviewAsync(input, id);
		return await _appService.UpdateAsync(id, input);
	}

	private async Task<SalaryTemplatePreviewDto> AttachPreviewAsync(SaveSalaryTemplateDto input, Guid? id = null,
		decimal sampleMonthlyAmount = 20000m)
	{
		input.SampleMonthlyAmount = sampleMonthlyAmount;
		input.SampleEffectiveMonth = new DateTime(2026, 9, 1);
		var preview = await _appService.PreviewAsync(new PreviewSalaryTemplateRequestDto
		{
			TemplateId = id, ExpectedActiveVersionId = input.ExpectedActiveVersionId, ConcurrencyStamp = input.ConcurrencyStamp,
			Rules = input.Rules, SalaryBasis = input.DefaultSalaryBasis, ProtectionPolicy = input.DefaultProtectionPolicy,
			MonthlyAmount = input.SampleMonthlyAmount, EffectiveMonth = input.SampleEffectiveMonth,
			PfApplicable = input.DefaultPfApplicable, PfRuleId = input.DefaultPfRuleId,
			EsiManagementMode = input.DefaultEsiManagementMode, EsiPolicyId = input.DefaultEsiPolicyId, EsiExternalReason = input.DefaultEsiExternalReason
		});
		input.PreviewToken = preview.PreviewToken;
		return preview;
	}

	private static SaveSalaryTemplateDto Save(string name, SalaryTemplateRuleDto[] rules) => new()
	{
		Name = name,
		Description = "Reusable salary setup",
		DefaultEsiManagementMode = "EXTERNAL",
		DefaultEsiExternalReason = "ESIC is outside this component-only fixture.",
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
