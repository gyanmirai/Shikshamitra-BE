using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Reports;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class HrReportingWorkspaceApi_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string Root = "/api/hr/reports";
	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task History_Query_Should_Filter_Owned_Runs_And_Reject_Unknown_Columns()
	{
		await PrepareAsync();
		var created = await Client.PostAsJsonAsync(Root + "/runs", new HrReportCreateRunDto { TemplateId = "builtin:staff-directory", Revision = 1 }, JsonOptions);
		created.EnsureSuccessStatusCode();
		var run = (await created.Content.ReadFromJsonAsync<HrReportRunDto>(JsonOptions))!;
		var input = new HrReportTablePageQueryDto { Take = 100, Criteria = new() {
			Search = run.Name,
			Filters = [new() { Column = "name", Type = "equals", Value = System.Text.Json.JsonSerializer.SerializeToElement(run.Name) }],
			Sorts = [new() { Column = "capturedAt", Direction = "desc" }] } };
		var response = await Client.PostAsJsonAsync(Root + "/runs/query", input, JsonOptions);
		response.EnsureSuccessStatusCode();
		var history = (await response.Content.ReadFromJsonAsync<HrReportRunHistoryDto>(JsonOptions))!;
		history.Items.ShouldContain(item => item.Id == run.Id);
		history.Items.ShouldAllBe(item => item.Name == run.Name);
		using var other = Factory.CreateClient();
		other.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, Guid.NewGuid().ToString());
		var otherResponse = await other.PostAsJsonAsync(Root + "/runs/query", input, JsonOptions);
		otherResponse.EnsureSuccessStatusCode();
		(await otherResponse.Content.ReadFromJsonAsync<HrReportRunHistoryDto>(JsonOptions))!.Items.ShouldBeEmpty();
		input.Criteria.Sorts[0].Column = "ownerUserId";
		(await Client.PostAsJsonAsync(Root + "/runs/query", input, JsonOptions)).IsSuccessStatusCode.ShouldBeFalse();
	}
	private async Task PrepareAsync()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		using var services = Factory.Services.CreateScope();
		using var tenant = services.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
		using var uow = services.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var users = services.ServiceProvider.GetRequiredService<IRepository<IdentityUser, Guid>>();
		var id = Guid.Parse(TestAuthHandler.TestUserId);
		if (await users.FindAsync(id) == null) await users.InsertAsync(new IdentityUser(id, "reporting-test", "reporting-test@example.invalid", Guid.Parse(TestAuthHandler.TestTenantId)));
		await uow.CompleteAsync();
	}
	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Reports_View_Alone_Should_Not_Discover_Or_Generate_Source_Data()
	{
		await PrepareAsync();
		using var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, ShikshaMitraSubmodulePermissions.HR.Reports.View);
		var catalogue = await client.GetFromJsonAsync<List<HrReportDatasetDto>>(Root + "/catalogue", JsonOptions);
		catalogue.ShouldBeEmpty();
		var options = await client.GetAsync(Root + "/workspace-options?dataset=payroll_register");
		options.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		var generation = await client.PostAsJsonAsync(Root + "/runs", new HrReportCreateRunDto { TemplateId = "builtin:salary-register", Revision = 1 }, JsonOptions);
		generation.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}
	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Revoking_Source_Access_Should_Block_An_Existing_Run()
	{
		await PrepareAsync();
		var response = await Client.PostAsJsonAsync(Root + "/runs", new HrReportCreateRunDto { TemplateId = "builtin:staff-directory", Revision = 1 }, JsonOptions);
		var run = (await response.Content.ReadFromJsonAsync<HrReportRunDto>(JsonOptions))!;
		using var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, ShikshaMitraSubmodulePermissions.HR.Reports.View + "," + ShikshaMitraSubmodulePermissions.HR.Reports.Export);
		(await client.GetAsync(Root + "/runs/" + run.Id)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await client.GetAsync(Root + "/runs/" + run.Id + "/rows")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await client.GetAsync(Root + "/runs/" + run.Id + "/download?format=excel")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}
	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task School_Drafts_Should_Keep_The_Published_Revision_And_Require_Template_Management()
	{
		await PrepareAsync();
		var input = new HrReportSaveDesignDto { Name = "School " + Guid.NewGuid().ToString("N")[..6], Visibility = "organization", Publish = true, Definition = Definition() };
		input.Definition.Filters.Conditions = [new() { Field = "department", Operator = "equals", Value = "Published" }];
		var createdResponse = await Client.PostAsJsonAsync(Root + "/library", input, JsonOptions);
		var created = (await createdResponse.Content.ReadFromJsonAsync<HrReportLibraryTemplateDto>(JsonOptions))!;
		created.PublishedRevision.ShouldBe(1);
		input.Publish = false; input.ConcurrencyStamp = created.ConcurrencyStamp; input.Definition.Filters.Conditions[0].Value = "Draft";
		var draftResponse = await Client.PutAsJsonAsync(Root + "/library/" + created.Id, input, JsonOptions);
		draftResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var draft = (await draftResponse.Content.ReadFromJsonAsync<HrReportLibraryTemplateDto>(JsonOptions))!;
		draft.Revision.ShouldBe(2); draft.PublishedRevision.ShouldBe(1);
		var published = (await Client.GetFromJsonAsync<List<HrReportLibraryTemplateDto>>(Root + "/library", JsonOptions))!.Single(x => x.Id == created.Id);
		published.Definition.Filters.Conditions[0].Value.ShouldBe("Published"); published.Revision.ShouldBe(1);
		using var designer = Factory.CreateClient();
		designer.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, string.Join(',', ShikshaMitraSubmodulePermissions.HR.Reports.View, ShikshaMitraSubmodulePermissions.HR.Reports.Build, ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View));
		input.ConcurrencyStamp = draft.ConcurrencyStamp;
		(await designer.PutAsJsonAsync(Root + "/library/" + created.Id, input, JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		var queued = await Client.PostAsJsonAsync(Root + "/runs", new HrReportCreateRunDto { TemplateId = created.Id, Revision = 1 }, JsonOptions);
		var run = (await queued.Content.ReadFromJsonAsync<HrReportRunDto>(JsonOptions))!;
		(await Client.PostAsync(Root + "/library/" + created.Id + "/archive?concurrencyStamp=" + draft.ConcurrencyStamp, null)).EnsureSuccessStatusCode();
		(await Client.GetAsync(Root + "/runs/" + run.Id)).StatusCode.ShouldBe(HttpStatusCode.OK);
		(await Client.PostAsJsonAsync(Root + "/runs", new HrReportCreateRunDto { TemplateId = created.Id, Revision = 1 }, JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}
	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Library_Should_Contain_Reusable_Immutable_Starters()
	{
		await PrepareAsync();
		var response = await Client.GetAsync(Root + "/library"); response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var templates = await response.Content.ReadFromJsonAsync<List<HrReportLibraryTemplateDto>>(JsonOptions);
		templates!.Count(x => x.IsBuiltIn).ShouldBe(20);
		templates.ShouldNotBeNull().ShouldContain(x => x.Id == "builtin:in-out" && x.Definition.Parameters.Contains("period"));
	}
	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Template_Should_Round_Trip_And_Reject_Stale_Updates()
	{
		await PrepareAsync();
		var body = new HrReportSaveDesignDto { Name = "Reusable " + Guid.NewGuid().ToString("N")[..6], Definition = Definition() };
		var response = await Client.PostAsJsonAsync(Root + "/library", body, JsonOptions); response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var created = (await response.Content.ReadFromJsonAsync<HrReportLibraryTemplateDto>(JsonOptions))!;
		created.Revision.ShouldBe(1); created.Definition.DefaultPeriod.ShouldBe("thisMonth");
		body.ConcurrencyStamp = created.ConcurrencyStamp; body.Name += " changed";
		var update = await Client.PutAsJsonAsync(Root + "/library/" + created.Id, body, JsonOptions); update.StatusCode.ShouldBe(HttpStatusCode.OK);
		var stale = await Client.PutAsJsonAsync(Root + "/library/" + created.Id, body, JsonOptions); stale.IsSuccessStatusCode.ShouldBeFalse();
	}
	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Fixed_Filter_Definition_Cannot_Be_Overridden_In_Generation()
	{
		await PrepareAsync();
		var body = new HrReportSaveDesignDto { Name = "Fixed " + Guid.NewGuid().ToString("N")[..6], Definition = Definition() };
		body.Definition.Filters.Conditions = [new() { Field = "employeeCode", Operator = "equals", Value = "does-not-exist" }];
		var saved = await Client.PostAsJsonAsync(Root + "/library", body, JsonOptions);
		var template = (await saved.Content.ReadFromJsonAsync<HrReportLibraryTemplateDto>(JsonOptions))!;
		var response = await Client.PostAsJsonAsync(Root + "/runs", new HrReportCreateRunDto { TemplateId = template.Id, Revision = template.Revision }, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var run = (await response.Content.ReadFromJsonAsync<HrReportRunDto>(JsonOptions))!;
		using var services = Factory.Services.CreateScope();
		using var tenant = services.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
		using var uow = services.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var stored = await services.ServiceProvider.GetRequiredService<IRepository<HrReportRun, Guid>>().GetAsync(run.Id);
		stored.DefinitionJson.ShouldContain("does-not-exist"); await uow.CompleteAsync();
	}
	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Capture_Should_Return_A_Background_Run_And_Reject_Unsaved_Client_Definitions()
	{
		await PrepareAsync();
		var response = await Client.PostAsJsonAsync(Root + "/runs", new HrReportCreateRunDto { TemplateId = "builtin:staff-directory", Revision = 1 }, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var run = (await response.Content.ReadFromJsonAsync<HrReportRunDto>(JsonOptions))!;
		run.Status.ShouldBe("Queued"); run.ExpiresAt.ShouldBeGreaterThan(run.CapturedAt);
		var bad = await Client.PostAsJsonAsync(Root + "/runs", new HrReportCreateRunDto { TemplateId = "builtin:staff-directory", Revision = 999 }, JsonOptions);
		bad.IsSuccessStatusCode.ShouldBeFalse();
	}
	[Theory]
	[InlineData("schedules")]
	[InlineData("templates")]
	[InlineData("ai/plan")]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Retired_Reporting_Endpoints_Should_Not_Be_Exposed(string route)
	{
		var response = await Client.GetAsync(Root + "/" + route); response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}
	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Anonymous_User_Cannot_Read_The_Library()
	{
		var response = await CreateAnonymousClient().GetAsync(Root + "/library"); response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}
	private static HrReportDesignDto Definition() => new() { DataSource = "employee", Columns = [new() { Field = "employeeCode" }, new() { Field = "employeeName" }], Parameters = [] };
	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task History_Should_Fill_Visible_Pages_After_Checking_Current_Permissions()
	{
		await PrepareAsync();
		var staff = await PostAndReadAsync<HrReportRunDto>(Root + "/runs", new HrReportCreateRunDto { TemplateId = "builtin:staff-directory", Revision = 1 });
		await PostAndReadAsync<HrReportRunDto>(Root + "/runs", new HrReportCreateRunDto { TemplateId = "builtin:salary-register", Revision = 1 });
		using var limited = Factory.CreateClient();
		limited.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, string.Join(',', ShikshaMitraSubmodulePermissions.HR.Reports.View, ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View));
		var hidden = (await limited.GetFromJsonAsync<HrReportRunHistoryDto>(Root + "/runs?take=1", JsonOptions))!;
		hidden.Items.Single().Id.ShouldBe(staff.Id);
		var older = (await limited.GetFromJsonAsync<HrReportRunHistoryDto>(Root + "/runs?skip=1&take=1", JsonOptions))!;
		older.Items.ShouldNotContain(item => item.Id == staff.Id);
		hidden.HasMore.ShouldBe(older.Items.Count > 0);
	}
}
