using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Payroll.Statutory;
using ShikshaMitra.HR.Payroll.Templates;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

[Collection(ApiTestCollection.Name)]
public class EsiPolicyArchiveApi_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string Url = "/api/payroll/statutory-schemes/esi/policies";
	private async Task InTenantAsync(Func<IServiceProvider, Task> action)
	{
		using var scope = Factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		await action(scope.ServiceProvider);
		await unit.CompleteAsync();
	}
	private Task<EsiPolicyDto> CreateAsync() => PostAndReadAsync<EsiPolicyDto>(Url, new CreateEsiPolicyDto
	{
		Name = "Archive Test " + Guid.NewGuid().ToString("N"), Code = "ARCHIVE_" + Guid.NewGuid().ToString("N"),
		EffectiveFrom = new(2046, 1, 1), SourceUrl = "https://example.invalid/test", SourceVerifiedOn = new(2026, 10, 1),
		IdempotencyKey = Guid.NewGuid(), CalculationSettings = new() { EligibilityBasis = "GROSS", CalculationBasis = "GROSS" }
	});
	[PayrollPostgresFact]
	public async Task Concurrent_template_save_and_archive_are_serialized_in_both_orders()
	{
		using var postgres = new PayrollPostgresTestFactory();
		var suite = new EsiPolicyArchiveApi_Tests(postgres);
		await suite.RaceAsync(postgres, saveFirst: true);
		await suite.RaceAsync(postgres, saveFirst: false);
	}
	private async Task RaceAsync(PayrollPostgresTestFactory postgres, bool saveFirst)
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var policy = await CreateAsync();
		var catalog = await GetAndReadAsync<SalaryTemplateComponentCatalogDto>("/api/payroll/salary-templates/component-catalog");
		var preview = new PreviewSalaryTemplateRequestDto { SalaryBasis = "GROSS", MonthlyAmount = 10000m,
			EffectiveMonth = new(2046, 4, 1), EsiPolicyId = policy.Id, Rules = [new SalaryTemplateRuleDto {
			ComponentId = catalog.Items.First(x => x.LineKind == "EARNING").ComponentId, SemanticType = "BASIC",
			LineKind = "EARNING", FormulaType = "PERCENTAGE_OF_GROSS", Value = 100m }] };
		var calculated = await PostAndReadAsync<SalaryTemplatePreviewDto>("/api/payroll/salary-templates/preview", preview);
		var save = new SaveSalaryTemplateDto { Name = "Archive race " + Guid.NewGuid().ToString("N"), Rules = preview.Rules,
			DefaultSalaryBasis = "GROSS", DefaultEsiPolicyId = policy.Id, SampleMonthlyAmount = 10000m,
			SampleEffectiveMonth = preview.EffectiveMonth, PreviewToken = calculated.PreviewToken, IdempotencyKey = Guid.NewGuid().ToString("N") };
		postgres.Barrier.Arm(saveFirst ? "HR_SalaryStructureTemplates" : "HR_SalaryStructureTemplateVersions");
		var first = saveFirst ? Client.PostAsJsonAsync("/api/payroll/salary-templates", save)
			: Client.PostAsJsonAsync($"{Url}/{policy.Id}/archive", new { policy.ConcurrencyStamp, confirmed = true });
		try
		{
			await postgres.Barrier.Reached.WaitAsync(TimeSpan.FromSeconds(30));
			postgres.Barrier.Observe("INSERT INTO \"HR_PayrollInputRevisions\"");
			var second = saveFirst ? Client.PostAsJsonAsync($"{Url}/{policy.Id}/archive", new { policy.ConcurrencyStamp, confirmed = true })
				: Client.PostAsJsonAsync("/api/payroll/salary-templates", save);
			await postgres.Barrier.Observed.WaitAsync(TimeSpan.FromSeconds(30));
			second.IsCompleted.ShouldBeFalse("The first write owns the tenant payroll fence.");
			postgres.Barrier.Release();
			(await first.WaitAsync(TimeSpan.FromSeconds(45))).IsSuccessStatusCode.ShouldBeTrue();
			var rejected = await second.WaitAsync(TimeSpan.FromSeconds(45));
			rejected.IsSuccessStatusCode.ShouldBeFalse();
			// A competing fence revision can reject the transaction with an optimistic
			// concurrency conflict before domain validation. A fresh retry must then
			// report the winning write's actual policy/dependency state.
			if (rejected.StatusCode == HttpStatusCode.Conflict)
			{
				var refreshed = await GetAndReadAsync<EsiPolicyArchiveReviewDto>($"{Url}/{policy.Id}/archive-review");
				rejected = saveFirst ? await Client.PostAsJsonAsync($"{Url}/{policy.Id}/archive", new { refreshed.Policy.ConcurrencyStamp, confirmed = true })
					: await Client.PostAsJsonAsync("/api/payroll/salary-templates", save);
			}
			var error = await rejected.Content.ReadAsStringAsync();
			Assert.True(error.Contains(saveFirst ? HRErrorCodes.EsiPolicyInUse : HRErrorCodes.EsiPolicyArchived, StringComparison.Ordinal), error);
			var review = await GetAndReadAsync<EsiPolicyArchiveReviewDto>($"{Url}/{policy.Id}/archive-review");
			review.Policy.IsArchived.ShouldBe(!saveFirst);
			review.Templates.Count.ShouldBe(saveFirst ? 1 : 0);
		}
		finally { postgres.Barrier.Release(); }
	}
	[Fact]
	public async Task Archive_requires_confirmation_rechecks_live_dependencies_and_preserves_history()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var policy = await CreateAsync();
		var review = await GetAndReadAsync<EsiPolicyArchiveReviewDto>($"{Url}/{policy.Id}/archive-review");
		review.CanArchive.ShouldBeTrue();
		review.Templates.ShouldBeEmpty();
		(await Client.PostAsJsonAsync($"{Url}/{policy.Id}/archive", new { policy.ConcurrencyStamp })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		Guid templateId = Guid.Empty;
		await InTenantAsync(async services => {
			var tenantId = services.GetRequiredService<ICurrentTenant>().Id;
			var template = new SalaryStructureTemplate(Guid.NewGuid(), tenantId, "DM Salary Template " + Guid.NewGuid().ToString("N"), null);
			templateId = template.Id;
			var version = new SalaryStructureTemplateVersion(Guid.NewGuid(), tenantId, template.Id, 1, []);
			version.SetEsiAssignment(EsiManagement.InPlatform, policy.Id, null);
			version.Publish(DateTime.UtcNow);
			template.MarkPublished(1);
			await services.GetRequiredService<IRepository<SalaryStructureTemplate, Guid>>().InsertAsync(template);
			await services.GetRequiredService<IRepository<SalaryStructureTemplateVersion, Guid>>().InsertAsync(version);
		});
		// An earlier clear review and a direct API call cannot bypass a new dependency.
		var blocked = await Client.PostAsJsonAsync($"{Url}/{policy.Id}/archive", new { policy.ConcurrencyStamp, confirmed = true });
		blocked.StatusCode.ShouldBe(HttpStatusCode.Conflict);
		(await blocked.Content.ReadAsStringAsync()).ShouldContain(HRErrorCodes.EsiPolicyInUse);
		review = await GetAndReadAsync<EsiPolicyArchiveReviewDto>($"{Url}/{policy.Id}/archive-review");
		review.CanArchive.ShouldBeFalse();
		review.Templates.Single().TemplateId.ShouldBe(templateId);
		review.Templates.Single().TemplateName.ShouldStartWith("DM Salary Template");
		review.Policy.Versions.Single().EffectiveFrom.ShouldBe(new DateTime(2046, 1, 1));
		await InTenantAsync(async services => {
			var repository = services.GetRequiredService<IRepository<SalaryStructureTemplate, Guid>>();
			var template = await repository.GetAsync(templateId);
			template.Archive();
			await repository.UpdateAsync(template);
		});
		(await Client.PostAsJsonAsync($"{Url}/{policy.Id}/archive", new { concurrencyStamp = "stale", confirmed = true })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
		(await Client.PostAsJsonAsync($"{Url}/{policy.Id}/archive", new { policy.ConcurrencyStamp, confirmed = true })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
		(await Client.PostAsJsonAsync($"{Url}/{policy.Id}/archive", new { policy.ConcurrencyStamp, confirmed = true })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
		review = await GetAndReadAsync<EsiPolicyArchiveReviewDto>($"{Url}/{policy.Id}/archive-review");
		review.Policy.IsArchived.ShouldBeTrue();
		review.CanArchive.ShouldBeFalse();
		review.Policy.Versions.Count.ShouldBe(1);
		await InTenantAsync(async services => {
			var resolution = await services.GetRequiredService<EsiPolicyResolver>().ResolveAsync(policy.Id, new(2046, 4, 1));
			resolution.Rule.Id.ShouldBe(review.Policy.Versions.Single().Id);
			(await services.GetRequiredService<IRepository<SalaryStructureTemplateVersion, Guid>>().GetListAsync(x => x.TemplateId == templateId))
				.Single().DefaultEsiPolicyId.ShouldBe(policy.Id);
		});
	}
	[Fact]
	public async Task Review_lists_all_live_selections_but_excludes_superseded_and_archived_templates()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var policy = await CreateAsync();
		await InTenantAsync(async services => {
			var tenant = services.GetRequiredService<ICurrentTenant>().Id;
			var templates = services.GetRequiredService<IRepository<SalaryStructureTemplate, Guid>>();
			var versions = services.GetRequiredService<IRepository<SalaryStructureTemplateVersion, Guid>>();
			foreach (var name in new[] { "Live draft", "Live published", "Superseded", "Archived" })
			{
				var template = new SalaryStructureTemplate(Guid.NewGuid(), tenant, name, null);
				var version = new SalaryStructureTemplateVersion(Guid.NewGuid(), tenant, template.Id, 1, []);
				version.SetEsiAssignment(EsiManagement.InPlatform, policy.Id, null);
				if (name != "Live draft") { version.Publish(DateTime.UtcNow); template.MarkPublished(1); }
				await templates.InsertAsync(template);
				await versions.InsertAsync(version);
				if (name == "Superseded")
				{
					var replacement = new SalaryStructureTemplateVersion(Guid.NewGuid(), tenant, template.Id, template.ReserveNextVersion(), []);
					replacement.SetEsiAssignment(EsiManagement.External, null, "Synthetic external replacement");
					replacement.Publish(DateTime.UtcNow);
					template.MarkPublished(2);
					await versions.InsertAsync(replacement);
				}
				if (name == "Archived") template.Archive();
				await templates.UpdateAsync(template);
			}
		});
		var review = await GetAndReadAsync<EsiPolicyArchiveReviewDto>($"{Url}/{policy.Id}/archive-review");
		review.CanArchive.ShouldBeFalse();
		review.Templates.Select(x => x.TemplateName).ShouldBe(new[] { "Live draft", "Live published" });
	}
	[Fact]
	public async Task Review_and_archive_enforce_management_scope_and_tenant_boundaries()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var policy = await CreateAsync();
		using var selection = Factory.CreateClient();
		selection.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
			ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.View);
		(await selection.GetAsync($"{Url}/{policy.Id}/archive-review")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await selection.PostAsJsonAsync($"{Url}/{policy.Id}/archive", new { policy.ConcurrencyStamp, confirmed = true })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		using var other = Factory.CreateClient();
		other.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, Guid.NewGuid().ToString());
		var otherReview = await other.GetAsync($"{Url}/{policy.Id}/archive-review");
		otherReview.IsSuccessStatusCode.ShouldBeFalse();
		(await otherReview.Content.ReadAsStringAsync()).ShouldNotContain(policy.Name);
	}
}
