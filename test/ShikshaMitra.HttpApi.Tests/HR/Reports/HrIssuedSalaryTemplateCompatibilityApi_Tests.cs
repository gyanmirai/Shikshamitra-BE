using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Reports;

/// <summary>SQLite API compatibility; queues generation without claiming PostgreSQL worker/concurrency acceptance.</summary>
[Collection(ApiTestCollection.Name)]
public class HrIssuedSalaryTemplateCompatibilityApi_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string Root = "/api/hr/reports";
	private static readonly Guid Tenant = Guid.Parse(TestAuthHandler.TestTenantId);
	private static readonly Guid Actor = Guid.Parse(TestAuthHandler.TestUserId);

	[Theory]
	[InlineData("salary-register")]
	[InlineData("salary-statement")]
	public async Task Existing_salary_definition_can_preview_queue_and_save_without_restoring_drafts(string starter)
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(Client, "LegacyIssuedReport");
		var definition = HrReportingCatalogue.BuiltIns().Single(x => x.Id == "builtin:" + starter).Definition;
		definition.Parameters.Add("includeDrafts");
		var storedJson = JsonSerializer.Serialize(definition, HrReportingEngine.Json);
		var templateId = Guid.NewGuid();
		await InTenantAsync(async services =>
		{
			var users = services.GetRequiredService<IRepository<IdentityUser, Guid>>();
			if (await users.FindAsync(Actor) == null) await users.InsertAsync(new(Actor, "legacy-report-fixture", "legacy-report@example.invalid", Tenant));
			var template = new HrReportTemplate(templateId, Tenant, Actor, "Legacy issued salary " + templateId.ToString("N")[..6],
				"Synthetic stored definition", "Payroll", definition.DataSource, "organization", "Draft", "{}");
			template.SaveDesign(template.Name, template.Description, template.Category, template.DataSource, template.Visibility, storedJson, true);
			await services.GetRequiredService<IRepository<HrReportTemplate, Guid>>().InsertAsync(template);
			await services.GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>().InsertAsync(new(Guid.NewGuid(), Tenant, "2026-10", employee.Id)
				{ EmployeeName = "Unlocked synthetic payroll", EmployeeIdentifier = "LEGACY-DRAFT", NetPay = 12345.67m, GrossSalary = 12345.67m });
		});
		var library = await Client.GetFromJsonAsync<System.Collections.Generic.List<HrReportLibraryTemplateDto>>(Root + "/library?edit=true", JsonOptions);
		var existing = library!.Single(x => x.Id == templateId.ToString());
		existing.Definition.Parameters.ShouldContain("includeDrafts");
		HrReportParametersDto Parameters(bool drafts = false) => new()
		{ Period = "month", Month = "2026-10", EmployeeIds = [employee.Id], IncludeDrafts = drafts };
		var preview = await ReadSuccessAsync<HrReportingPreviewDto>(Root + "/designer-preview", new HrReportDesignerPreviewDto
		{ Definition = existing.Definition, Parameters = Parameters() });
		preview.Rows.Any(row => row.Values.Any(value => value?.ToString() == "12345.67")).ShouldBeFalse();
		preview.Rows.Any(row => row.TryGetValue("status", out var value) && value?.ToString() is "Draft" or "Preview").ShouldBeFalse();
		if (starter == "salary-register") preview.TotalCount.ShouldBe(0);
		else preview.Rows.Single()["status"]!.ToString().ShouldBe("Unavailable");

		// Resolve the actual published legacy JSON again, independently of the preview request's normalization.
		var run = await ReadSuccessAsync<HrReportRunDto>(Root + "/runs", new HrReportCreateRunDto
		{ TemplateId = existing.Id, Revision = existing.Revision, Parameters = Parameters() });
		run.Status.ShouldBe("Queued");
		run.Parameters.IncludeDrafts.ShouldBeFalse();
		await InTenantAsync(async services =>
		{
			var original = await services.GetRequiredService<IRepository<HrReportTemplate, Guid>>().GetAsync(templateId);
			original.PublishedDefinitionJson.ShouldBe(storedJson, "Read/preview/queue must not rewrite the saved published revision.");
			var captured = await services.GetRequiredService<IRepository<HrReportRun, Guid>>().GetAsync(run.Id);
			JsonSerializer.Deserialize<HrReportDesignDto>(captured.DefinitionJson, HrReportingEngine.Json)!.Parameters.ShouldNotContain("includeDrafts");
			JsonSerializer.Deserialize<HrReportParametersDto>(captured.ParametersJson, HrReportingEngine.Json)!.IncludeDrafts.ShouldBeFalse();
		});
		var deniedPreview = await Client.PostAsJsonAsync(Root + "/designer-preview", new HrReportDesignerPreviewDto
		{ Definition = JsonSerializer.Deserialize<HrReportDesignDto>(storedJson, HrReportingEngine.Json)!, Parameters = Parameters(true) }, JsonOptions);
		deniedPreview.IsSuccessStatusCode.ShouldBeFalse();
		(await deniedPreview.Content.ReadAsStringAsync()).ShouldContain("The template does not expose this run parameter.");
		var deniedRun = await Client.PostAsJsonAsync(Root + "/runs", new HrReportCreateRunDto
		{ TemplateId = existing.Id, Revision = existing.Revision, Parameters = Parameters(true) }, JsonOptions);
		deniedRun.IsSuccessStatusCode.ShouldBeFalse();

		var saved = await ReadSuccessAsync<HrReportLibraryTemplateDto>(Root + "/library/" + templateId, new HrReportSaveDesignDto
		{ Name = existing.Name, Description = existing.Description, Visibility = existing.Visibility, ConcurrencyStamp = existing.ConcurrencyStamp,
			Definition = JsonSerializer.Deserialize<HrReportDesignDto>(storedJson, HrReportingEngine.Json)!, Publish = true }, update: true);
		saved.Definition.Parameters.ShouldNotContain("includeDrafts");
		saved.Revision.ShouldBe(existing.Revision + 1);
		await InTenantAsync(async services =>
		{
			var persisted = await services.GetRequiredService<IRepository<HrReportTemplate, Guid>>().GetAsync(templateId);
			JsonSerializer.Deserialize<HrReportDesignDto>(persisted.PublishedDefinitionJson!, HrReportingEngine.Json)!.Parameters.ShouldNotContain("includeDrafts");
		});
	}

	private async Task<T> ReadSuccessAsync<T>(string path, object body, bool update = false)
	{
		using var response = update ? await Client.PutAsJsonAsync(path, body, JsonOptions) : await Client.PostAsJsonAsync(path, body, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
	}

	private async Task InTenantAsync(Func<IServiceProvider, Task> action)
	{
		using var scope = Factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Tenant);
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		await action(scope.ServiceProvider);
		await unit.CompleteAsync();
	}
}
