using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Payroll.Templates;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;
using H = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.HR;

namespace ShikshaMitra.HR.Payroll;

/// <summary>Real template writes against the existing SQLite fixture retain independent action grants.</summary>
public class PayrollSettingsActionWorkflow_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Fact]
	public async Task Template_writes_and_replays_do_not_add_a_view_requirement_and_revocation_is_fresh()
	{
		var tenant = GetRequiredService<ICurrentTenant>();
		using var tenantScope = tenant.Change(Guid.NewGuid());
		var component = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<EarningComponent, Guid>>().InsertAsync(
			new EarningComponent(Guid.NewGuid(), tenant.Id, "Synthetic authority Basic", null, EarningComponentType.Fixed,
				CalculationType.Amount, 0m, null, null, ComponentDependency.None, true), autoSave: true));
		string? allowed = null;
		var checkedPolicies = new List<string>();
		var authorization = Substitute.For<IAbpAuthorizationService>();
		authorization.CurrentPrincipal.Returns(new ClaimsPrincipal(new ClaimsIdentity()));
		authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<string>()).Returns(call =>
		{
			var policy = call.ArgAt<string>(2);
			checkedPolicies.Add(policy);
			return Task.FromResult(policy == allowed ? AuthorizationResult.Success() : AuthorizationResult.Failed());
		});
		var service = ActivatorUtilities.CreateInstance<SalaryTemplateAppService>(ServiceProvider);
		service.LazyServiceProvider = new AbpLazyServiceProvider(new AuthorizationProvider(ServiceProvider, authorization));
		var input = new SaveSalaryTemplateDto
		{
			Name = "Synthetic independent authority", Description = "In-memory authorization regression",
			DefaultSalaryBasis = "EMPLOYER_COST", DefaultProtectionPolicy = "PROTECT_EMPLOYER_COST",
			DefaultEsiManagementMode = "EXTERNAL", DefaultEsiExternalReason = "Synthetic component-only fixture",
			IdempotencyKey = Guid.NewGuid().ToString("N"),
			Rules = [new() { ComponentId = component.Id, LineKind = "EARNING", SemanticType = "BASIC", FormulaType = "PERCENTAGE_OF_GROSS", Value = 100m }]
		};

		async Task AttachPreviewAsync(Guid? id = null)
		{
			allowed = H.PayrollManagement_SettingsSalaryTemplates.View;
			input.SampleMonthlyAmount = 20000m;
			input.SampleEffectiveMonth = new DateTime(2026, 9, 1);
			var preview = await WithUnitOfWorkAsync(() => service.PreviewAsync(new()
			{
				TemplateId = id, ExpectedActiveVersionId = input.ExpectedActiveVersionId, ConcurrencyStamp = input.ConcurrencyStamp,
				Rules = input.Rules, SalaryBasis = input.DefaultSalaryBasis, ProtectionPolicy = input.DefaultProtectionPolicy,
				MonthlyAmount = input.SampleMonthlyAmount, EffectiveMonth = input.SampleEffectiveMonth,
				PfApplicable = input.DefaultPfApplicable, EsiManagementMode = input.DefaultEsiManagementMode,
				EsiExternalReason = input.DefaultEsiExternalReason
			}));
			input.PreviewToken = preview.PreviewToken;
			checkedPolicies.Clear();
		}

		await AttachPreviewAsync();
		allowed = H.PayrollManagement_SettingsSalaryTemplates.Add;
		var created = await WithUnitOfWorkAsync(() => service.CreateAsync(input));
		var replay = await WithUnitOfWorkAsync(() => service.CreateAsync(input));
		replay.Id.ShouldBe(created.Id);
		replay.VersionCount.ShouldBe(1);
		checkedPolicies.ShouldBe(new[] { allowed, allowed });

		input.IdempotencyKey = Guid.NewGuid().ToString("N");
		input.Name += " revised";
		input.ExpectedActiveVersionId = created.EditableVersion.Id;
		input.ConcurrencyStamp = created.ConcurrencyStamp;
		await AttachPreviewAsync(created.Id);
		allowed = H.PayrollManagement_SettingsSalaryTemplates.Edit;
		var updated = await WithUnitOfWorkAsync(() => service.UpdateAsync(created.Id, input));
		var updateReplay = await WithUnitOfWorkAsync(() => service.UpdateAsync(created.Id, input));
		updateReplay.EditableVersion.Id.ShouldBe(updated.EditableVersion.Id);
		updated.VersionCount.ShouldBe(2);
		checkedPolicies.ShouldBe(new[] { allowed, allowed });

		checkedPolicies.Clear();
		allowed = H.PayrollManagement_SettingsSalaryTemplates.SetDefault;
		(await WithUnitOfWorkAsync(() => service.SetDefaultAsync(created.Id))).IsDefault.ShouldBeTrue();
		checkedPolicies.ShouldBe(new[] { allowed });
		checkedPolicies.Clear();
		allowed = H.PayrollManagement_CompensationRevisions.Manage;
		(await WithUnitOfWorkAsync(() => service.GetActiveListAsync())).Items.Single().Id.ShouldBe(created.Id);
		checkedPolicies.ShouldBe(new[] { allowed });

		allowed = H.PayrollManagement_SettingsSalaryTemplates.View;
		using (tenant.Change(Guid.NewGuid()))
			(await WithUnitOfWorkAsync(() => service.GetListAsync())).Items.ShouldBeEmpty();
		allowed = null;
		await Should.ThrowAsync<AbpAuthorizationException>(() => service.SetDefaultAsync(created.Id));
		await Should.ThrowAsync<AbpAuthorizationException>(() => service.GetAsync(created.Id));
	}

	private sealed class AuthorizationProvider(IServiceProvider services, IAuthorizationService authorization) : IServiceProvider
	{
		public object? GetService(Type type) => type == typeof(IAuthorizationService) ? authorization : services.GetService(type);
	}
}
