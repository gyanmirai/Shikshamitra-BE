using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Academic.Reports;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Reports;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public partial class InstituteReportApi_Tests
{
	[Fact]
	public async Task Exact_single_division_prompt_plans_previews_and_exports_only_matching_classes()
	{
		var (client, tenant, user) = await ClientAsync(); using var http = client;
		var (year, own, _) = await SeedHierarchyAsync(tenant);
		using (var scope = factory.Services.CreateScope())
		{
			using var school = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenant);
			using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
			var division = await scope.ServiceProvider.GetRequiredService<IRepository<Division, Guid>>().GetAsync(own);
			var standards = scope.ServiceProvider.GetRequiredService<IRepository<Standard, Guid>>();
			var original = await standards.GetAsync(division.StandardId);
			var single = Create<Standard>(Guid.NewGuid(), tenant, "Class 2", 6, original.EducationMediumStageId, null!);
			await standards.InsertAsync(single, autoSave: true);
			await standards.InsertAsync(Create<Standard>(Guid.NewGuid(), tenant, "Class 3", 7, original.EducationMediumStageId, null!), autoSave: true);
			await scope.ServiceProvider.GetRequiredService<IRepository<Division, Guid>>().InsertAsync(Create<Division>(Guid.NewGuid(), tenant, "A", single.Id, 30, ""));
			await unit.CompleteAsync();
		}
		factory.AiResult = new() { Summary = "Classes with exactly one permitted division.", Plan = new() {
			ReportType = "class-division-summary", Format = "csv", Filters = new() { AcademicYearId = year },
			Conditions = [new() { Field = "divisionCount", Operator = "eq", Value = "1" }]
		} };
		var response = await client.PostAsJsonAsync(Url + "/ai/plan", new InstituteAiPlanInput { Prompt = "can you tell me which class has only one division", AcademicYearId = year });
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var plan = (await response.Content.ReadFromJsonAsync<InstituteAiPlanResult>())!;
		var approved = new InstituteAiApprovedRequest { Plan = plan.Plan!, ApprovalToken = plan.ApprovalToken! };
		var previewResponse = await client.PostAsJsonAsync(Url + "/ai/preview", approved);
		previewResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await previewResponse.Content.ReadAsStringAsync());
		var preview = (await previewResponse.Content.ReadFromJsonAsync<AcademicReportPreviewDto>())!;
		preview.Rows.Single()["standard"].ShouldBe("Class 2");
		preview.Rows.Single()["divisionCount"].ShouldBe("1");
		preview.Rows.Single()["divisions"].ShouldBe("A");
		preview.FilterSummary.ShouldContain("only permitted divisions");
		var queued = await client.PostAsJsonAsync(Url + "/ai/exports", approved);
		queued.StatusCode.ShouldBe(HttpStatusCode.OK, await queued.Content.ReadAsStringAsync());
		var job = (await queued.Content.ReadFromJsonAsync<InstituteReportJobDto>())!;
		using (var scope = factory.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<InstituteReportExportJob>().ExecuteAsync(new() { JobId = job.Id, TenantId = tenant });
		var csv = Encoding.UTF8.GetString(await client.GetByteArrayAsync($"{Url}/exports/{job.Id}/download"));
		csv.ShouldContain("Class 2"); csv.ShouldNotContain("Class 1"); csv.ShouldNotContain("Class 3");
		factory.Scopes[user] = new UserScopeContext { UserId = user, TenantId = tenant, AcademicYearIds = [year], DivisionIds = [own] };
		var scoped = await client.PostAsJsonAsync(Url + "/preview", approved.Plan);
		(await scoped.Content.ReadFromJsonAsync<AcademicReportPreviewDto>())!.Rows.Single()["standard"].ShouldBe("Class 1");
		(await client.GetAsync($"{Url}/exports/{job.Id}/download")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		factory.Scopes[user] = new UserScopeContext { UserId = user, TenantId = tenant };
		(await client.PostAsJsonAsync(Url + "/preview", approved.Plan)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}
	[Theory]
	[InlineData("", "stop", "InstituteReports:AiInvalidResponse")]
	[InlineData("", "length", "InstituteReports:AiResponseIncomplete")]
	[InlineData("not a JSON plan", "stop", "InstituteReports:AiInvalidResponse")]
	public async Task Zai_empty_truncated_or_malformed_output_has_an_institute_error(string content, string finish, string code)
	{
		await using var zai = new InstituteReportTestFactory { Provider = "ZAI" };
		var (client, _, _) = await new InstituteReportApi_Tests(zai).ClientAsync(); using var http = client;
		zai.RawAiResponse = JsonSerializer.Serialize(new { choices = new[] { new { finish_reason = finish, message = new { content } } } });
		var response = await client.PostAsJsonAsync(Url + "/ai/plan", new InstituteAiPlanInput { Prompt = "can you tell me which class has only one division" });
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var error = await response.Content.ReadAsStringAsync();
		error.ShouldContain(code); error.ShouldNotContain("Curriculum"); error.ShouldNotContain("Academic:Reports:");
		using var payload = JsonDocument.Parse(zai.LastAiRequest!);
		payload.RootElement.GetProperty("thinking").GetProperty("type").GetString().ShouldBe("disabled");
		payload.RootElement.GetProperty("messages")[0].GetProperty("content").GetString()!.ShouldContain("divisionCount");
	}
}
