using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using ShikshaMitra.Academic.Reports;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.Reports;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public class InstituteReportTestFactory : SchoolDocumentsTestFactory
{
	public Dictionary<Guid, UserScopeContext> Scopes { get; } = [];
	public InstituteAiPlanResult AiResult { get; set; } = new();
	public string Provider { get; init; } = "OpenAI";
	public string? RawAiResponse { get; set; }
	public string? LastAiRequest { get; private set; }
	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		base.ConfigureWebHost(builder);
		builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["AI:Provider"] = Provider, ["OpenAI:ApiKey"] = "synthetic-test-key", ["ZAI:ApiKey"] = "synthetic-test-key" }));
		builder.ConfigureTestServices(services => {
			var scopes = Substitute.For<IUserScopeService>();
			scopes.GetUserScopeAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(call => Task.FromResult(Scopes.GetValueOrDefault(call.ArgAt<Guid>(0)) ?? new UserScopeContext()));
			services.AddSingleton(scopes);
			var clients = Substitute.For<IHttpClientFactory>();
			clients.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(new InstituteReportAiHandler(() => {
				var text = JsonSerializer.Serialize(AiResult, new JsonSerializerOptions(JsonSerializerDefaults.Web));
				return RawAiResponse ?? (Provider == "ZAI" ? JsonSerializer.Serialize(new { choices = new[] { new { finish_reason = "stop", message = new { content = text } } } })
					: JsonSerializer.Serialize(new { output = new[] { new { content = new[] { new { type = "output_text", text } } } } }));
			}, value => LastAiRequest = value)));
			services.AddSingleton(clients);
		});
	}
}

public class InstituteReportAiHandler(Func<string> response, Action<string> capture) : HttpMessageHandler
{
	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		capture(await request.Content!.ReadAsStringAsync(cancellationToken));
		return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response(), System.Text.Encoding.UTF8, "application/json") };
	}
}

[Trait("Module", "TenantSettings"), Trait("Tier", "Critical")]
public partial class InstituteReportApi_Tests(InstituteReportTestFactory factory) : IClassFixture<InstituteReportTestFactory>
{
	private const string Url = "/api/app/institute-setup-report";
	[Fact]
	public async Task Permission_catalogue_defines_tenant_view_generate_and_separate_navigation_without_delete()
	{
		using var scope = factory.Services.CreateScope();
		var definitions = scope.ServiceProvider.GetRequiredService<IPermissionDefinitionManager>();
		foreach (var name in new[] { InstituteReportPermissions.View, InstituteReportPermissions.Add, InstituteReportPermissions.Navigation })
			(await definitions.GetAsync(name)).MultiTenancySide.ShouldBe(Volo.Abp.MultiTenancy.MultiTenancySides.Tenant);
		(await definitions.GetOrNullAsync("InstituteSetup.Reports.Delete")).ShouldBeNull();
	}
	[Fact]
	public async Task Background_execution_rejects_an_owner_disabled_after_queuing()
	{
		var (client, tenant, user) = await ClientAsync(); using var http = client;
		var response = await client.PostAsJsonAsync(Url + "/exports", new InstituteReportRequest { ReportType = "shift-register" });
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var job = (await response.Content.ReadFromJsonAsync<InstituteReportJobDto>())!;
		using (var scope = factory.Services.CreateScope())
		{
			using var school = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenant);
			using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
			var users = scope.ServiceProvider.GetRequiredService<IRepository<IdentityUser, Guid>>();
			var owner = await users.GetAsync(user); owner.SetIsActive(false); await users.UpdateAsync(owner); await unit.CompleteAsync();
		}
		using (var scope = factory.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<InstituteReportExportJob>().ExecuteAsync(new() { JobId = job.Id, TenantId = tenant });
		using (var scope = factory.Services.CreateScope())
		{
			using var school = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenant);
			using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
			(await scope.ServiceProvider.GetRequiredService<IRepository<InstituteReportJob, Guid>>().GetAsync(job.Id)).Status.ShouldBe("Failed");
		}
	}
	[Fact]
	public async Task Expired_files_are_unavailable_and_retention_removes_the_private_blob_reference()
	{
		var (client, tenant, _) = await ClientAsync(); using var http = client;
		var response = await client.PostAsJsonAsync(Url + "/exports", new InstituteReportRequest { ReportType = "shift-register", Format = "csv" });
		var job = (await response.Content.ReadFromJsonAsync<InstituteReportJobDto>())!;
		using (var scope = factory.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<InstituteReportExportJob>().ExecuteAsync(new() { JobId = job.Id, TenantId = tenant });
		using (var scope = factory.Services.CreateScope())
		{
			using var school = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenant);
			using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
			var repository = scope.ServiceProvider.GetRequiredService<IRepository<InstituteReportJob, Guid>>();
			var record = await repository.GetAsync(job.Id);
			typeof(InstituteReportJob).GetProperty(nameof(InstituteReportJob.ExpiresAt))!.SetValue(record, DateTime.UtcNow.AddMinutes(-1));
			await repository.UpdateAsync(record); await unit.CompleteAsync();
		}
		(await client.GetAsync($"{Url}/exports/{job.Id}/download")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		using (var scope = factory.Services.CreateScope())
		{
			using var school = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenant);
			await scope.ServiceProvider.GetRequiredService<InstituteReportRetention>().PurgeAsync();
			using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
			(await scope.ServiceProvider.GetRequiredService<IRepository<InstituteReportJob, Guid>>().GetAsync(job.Id)).BlobName.ShouldBeNull();
		}
	}
	[Theory]
	[InlineData("pdf", "application/pdf")]
	[InlineData("excel", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
	[InlineData("csv", "text/csv")]
	public async Task School_catalogue_preview_background_generation_and_authenticated_download(string format, string contentType)
	{
		var (client, tenant, user) = await ClientAsync(); using var http = client;
		var options = await client.GetFromJsonAsync<InstituteReportOptions>(Url + "/options");
		options!.ReportTypes.ShouldContain(x => x.Id == "shift-register");
		var request = new InstituteReportRequest { ReportType = "shift-register", Format = format };
		var preview = await client.PostAsJsonAsync(Url + "/preview", request);
		preview.StatusCode.ShouldBe(HttpStatusCode.OK, await preview.Content.ReadAsStringAsync());
		(await preview.Content.ReadFromJsonAsync<AcademicReportPreviewDto>())!.Rows.Single()["shift"].ShouldBe("Morning");
		var response = await client.PostAsJsonAsync(Url + "/exports", request);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var job = (await response.Content.ReadFromJsonAsync<InstituteReportJobDto>())!;
		(await client.GetAsync($"{Url}/exports/{job.Id}/download")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		using (var scope = factory.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<InstituteReportExportJob>().ExecuteAsync(new() { JobId = job.Id, TenantId = tenant });
		var finished = (await client.GetFromJsonAsync<InstituteReportJobDto>($"{Url}/exports/{job.Id}"))!;
		finished.Status.ShouldBe("Completed", finished.ErrorMessage);
		var file = await client.GetAsync($"{Url}/exports/{job.Id}/download");
		file.StatusCode.ShouldBe(HttpStatusCode.OK, await file.Content.ReadAsStringAsync());
		file.Content.Headers.ContentType!.MediaType.ShouldBe(contentType);
		var bytes = await file.Content.ReadAsByteArrayAsync(); bytes.Length.ShouldBeGreaterThan(10);
		if (format == "pdf") System.Text.Encoding.ASCII.GetString(bytes[..5]).ShouldBe("%PDF-");
		if (format == "excel") { using var workbook = new ClosedXML.Excel.XLWorkbook(new MemoryStream(bytes)); workbook.Worksheets.First().Cell(2, 1).GetString().ShouldBe("Morning"); }
		if (format == "csv") System.Text.Encoding.UTF8.GetString(bytes).ShouldContain("Morning");
		var (other, _, _) = await ClientAsync(); using var otherHttp = other;
		(await other.GetAsync($"{Url}/exports/{job.Id}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await other.GetFromJsonAsync<List<InstituteReportJobDto>>(Url + "/exports"))!.ShouldBeEmpty();
		client.DefaultRequestHeaders.Remove(TestAuthHandler.TestUserIdHeader);
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, Guid.NewGuid().ToString());
		(await client.GetAsync($"{Url}/exports/{job.Id}/download")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}
	[Fact]
	public async Task View_does_not_allow_generation_and_report_grants_do_not_replace_source_permissions()
	{
		var (client, _, _) = await ClientAsync(); using var http = client;
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, InstituteReportPermissions.View);
		(await client.GetFromJsonAsync<InstituteReportOptions>(Url + "/options"))!.ReportTypes.ShouldBeEmpty();
		(await client.PostAsJsonAsync(Url + "/preview", new InstituteReportRequest { ReportType = "shift-register" })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await client.PostAsJsonAsync(Url + "/exports", new InstituteReportRequest { ReportType = "shift-register" })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await client.PostAsJsonAsync(Url + "/ai/plan", new InstituteAiPlanInput { Prompt = "List every shift" })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}
	[Fact]
	public async Task Ai_plan_clarification_and_approval_are_validated_before_any_export()
	{
		var (client, _, _) = await ClientAsync(); using var http = client;
		factory.AiResult = new InstituteAiPlanResult { Status = "NeedsClarification", ClarificationQuestions = [new() { Id = "scope", Question = "Which catalogue?", Options = ["Shifts", "Rooms"], RecommendedOption = "Shifts" }] };
		var clarification = await client.PostAsJsonAsync(Url + "/ai/plan", new InstituteAiPlanInput { Prompt = "Prepare my setup report" });
		clarification.StatusCode.ShouldBe(HttpStatusCode.OK, await clarification.Content.ReadAsStringAsync());
		(await clarification.Content.ReadFromJsonAsync<InstituteAiPlanResult>())!.Plan.ShouldBeNull();
		factory.AiResult = new InstituteAiPlanResult { Plan = new() { ReportType = "shift-register", Format = "csv", Columns = ["shift", "start", "end"] }, Summary = "List the school shifts." };
		var planned = await client.PostAsJsonAsync(Url + "/ai/plan", new InstituteAiPlanInput { Prompt = "List the school shifts as CSV" });
		planned.StatusCode.ShouldBe(HttpStatusCode.OK, await planned.Content.ReadAsStringAsync());
		var result = (await planned.Content.ReadFromJsonAsync<InstituteAiPlanResult>())!;
		result.ApprovalToken.ShouldNotBeNullOrWhiteSpace();
		var approved = new InstituteAiApprovedRequest { Plan = result.Plan!, ApprovalToken = result.ApprovalToken! };
		(await client.PostAsJsonAsync(Url + "/ai/preview", approved)).StatusCode.ShouldBe(HttpStatusCode.OK);
		approved.Plan.Format = "excel";
		(await client.PostAsJsonAsync(Url + "/ai/exports", approved)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		approved.Plan.Format = "csv";
		var exported = await client.PostAsJsonAsync(Url + "/ai/exports", approved);
		exported.StatusCode.ShouldBe(HttpStatusCode.OK, await exported.Content.ReadAsStringAsync());
		(await exported.Content.ReadFromJsonAsync<InstituteReportJobDto>())!.IsDynamic.ShouldBeTrue();
		factory.AiResult.Plan!.Columns = ["salary"];
		(await client.PostAsJsonAsync(Url + "/ai/plan", new InstituteAiPlanInput { Prompt = "List the school shifts as CSV" })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}
	[Fact]
	public async Task Hierarchy_preview_is_filtered_before_pagination_and_revoked_scope_blocks_saved_files()
	{
		var (client, tenant, user) = await ClientAsync(); using var http = client;
		var (year, own, other) = await SeedHierarchyAsync(tenant);
		factory.Scopes[user] = new UserScopeContext { UserId = user, TenantId = tenant, AcademicYearIds = [year], DivisionIds = [own] };
		var request = new InstituteReportRequest { ReportType = "academic-hierarchy", Filters = new() { AcademicYearId = year }, PageSize = 1 };
		var response = await client.PostAsJsonAsync(Url + "/preview", request);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var preview = (await response.Content.ReadFromJsonAsync<AcademicReportPreviewDto>())!;
		preview.TotalCount.ShouldBe(1); preview.Rows.Single()["division"].ShouldBe("A");
		var queued = await client.PostAsJsonAsync(Url + "/exports", request);
		var job = (await queued.Content.ReadFromJsonAsync<InstituteReportJobDto>())!;
		using (var scope = factory.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<InstituteReportExportJob>().ExecuteAsync(new() { JobId = job.Id, TenantId = tenant });
		(await client.GetAsync($"{Url}/exports/{job.Id}/download")).StatusCode.ShouldBe(HttpStatusCode.OK);
		factory.Scopes[user] = new UserScopeContext { UserId = user, TenantId = tenant, AcademicYearIds = [year], DivisionIds = [other] };
		(await client.GetAsync($"{Url}/exports/{job.Id}/download")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}
	private async Task<(HttpClient Client, Guid Tenant, Guid User)> ClientAsync()
	{
		var tenant = Guid.NewGuid(); var user = Guid.NewGuid(); await factory.SeedTenantAsync(tenant);
		using (var scope = factory.Services.CreateScope())
		{
			using var school = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenant);
			using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
			await scope.ServiceProvider.GetRequiredService<IRepository<IdentityUser, Guid>>().InsertAsync(new IdentityUser(user, "report-user", "reports@example.test", tenant));
			var shift = new ShiftManagement { TenantId = tenant, ShiftName = "Morning", StartTime = new(8, 0), EndTime = new(12, 0) };
			typeof(ShiftManagement).GetProperty(nameof(ShiftManagement.Id))!.SetValue(shift, Guid.NewGuid());
			await scope.ServiceProvider.GetRequiredService<IRepository<ShiftManagement, Guid>>().InsertAsync(shift);
			await unit.CompleteAsync();
		}
		factory.Scopes[user] = new UserScopeContext { UserId = user, TenantId = tenant, HasWildcardAcademicYear = true };
		var client = factory.CreateClient(); client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, tenant.ToString()); client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, user.ToString());
		return (client, tenant, user);
	}
	private async Task<(Guid Year, Guid Own, Guid Other)> SeedHierarchyAsync(Guid tenant)
	{
		using var scope = factory.Services.CreateScope(); using var school = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenant);
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var year = Create<AcademicYear>(Guid.NewGuid(), tenant, "2026-2027", DateTime.UtcNow, DateTime.UtcNow.AddYears(1));
		await Insert(year);
		var board = Create<EducationBoard>(Guid.NewGuid(), tenant, (ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards.EducationBoardType)0, year.Id); await Insert(board);
		var medium = Create<EducationMedium>(Guid.NewGuid(), tenant, "English", board.Id); await Insert(medium);
		var stage = Create<EducationMediumStage>(Guid.NewGuid(), tenant, "Primary", null!, medium.Id); await Insert(stage);
		var standard = Create<Standard>(Guid.NewGuid(), tenant, "Class 1", 5, stage.Id, null!); await Insert(standard);
		var own = Create<Division>(Guid.NewGuid(), tenant, "A", standard.Id, 30, ""); await Insert(own);
		var other = Create<Division>(Guid.NewGuid(), tenant, "B", standard.Id, 30, ""); await Insert(other);
		await unit.CompleteAsync(); return (year.Id, own.Id, other.Id);
		async Task Insert<T>(T entity) where T : class, Volo.Abp.Domain.Entities.IEntity<Guid> => await scope.ServiceProvider.GetRequiredService<IRepository<T, Guid>>().InsertAsync(entity, autoSave: true);
	}
	private static T Create<T>(params object[] args) => (T)Activator.CreateInstance(typeof(T), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, args, null)!;
}
