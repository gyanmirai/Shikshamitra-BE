using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Academic.Setup;

[Collection(InitialSetupTestCollection.Name), Trait("Module", "Academic"), Trait("Tier", "Critical")]
public partial class AcademicSetupGuideApi_Tests(InitialSetupTestFactory factory)
{
	private const string Base = "/api/academics/setup";
	private const string SubjectView = "Academics.CurriculumManagement.Settings.SubjectCreation.View";
	private sealed record School(Guid TenantId, Guid ActorId, Guid YearId, Guid MediumId);

	[Fact]
	public async Task Only_first_configuration_entry_in_a_school_auto_opens_and_reads_do_not_record_visits()
	{
		var school = await SeedAsync(); using var first = As(school);
		(await ReadAsync(first, Base + "/visit-status")).GetProperty("hasVisitedConfiguration").GetBoolean().ShouldBeFalse();
		(await ReadAsync(first, Base + "/visit-status")).GetProperty("hasVisitedConfiguration").GetBoolean().ShouldBeFalse();
		var recorded = await SendAsync(first, HttpMethod.Post, Base + "/visit", new { settingKey = "subjects" });
		recorded.GetProperty("firstVisit").GetBoolean().ShouldBeTrue();
		var firstVisitedAt = recorded.GetProperty("firstVisitedAt").GetString();
		Guid secondId = default;
		await InAsync(school.TenantId, async services => {
			var actor = new IdentityUser(Guid.NewGuid(), "second", "second@example.test", school.TenantId);
			await services.GetRequiredService<IIdentityUserRepository>().InsertAsync(actor, true); secondId = actor.Id;
		});
		using var second = As(school with { ActorId = secondId });
		var repeated = await SendAsync(second, HttpMethod.Post, Base + "/visit", new { settingKey = "welcome" });
		repeated.GetProperty("firstVisit").GetBoolean().ShouldBeFalse();
		repeated.GetProperty("firstVisitedAt").GetString().ShouldBe(firstVisitedAt);
		(await ReadAsync(first, StateUrl(school))).GetProperty("settings").EnumerateArray()
			.ShouldAllBe(x => x.GetProperty("configurationState").GetString() != "configured");
		(await ReadAsync(second, Base + "/visit-status")).GetProperty("hasVisitedConfiguration").GetBoolean().ShouldBeTrue();
	}

	[Fact]
	public async Task Visit_authorization_uses_exact_setting_view_without_requiring_module_visibility()
	{
		var school = await SeedAsync(); using var client = As(school, SubjectView);
		(await ReadAsync(client, Base + "/visit-status")).GetProperty("canAccessSetup").GetBoolean().ShouldBeTrue();
		(await client.PostAsJsonAsync(Base + "/visit", new { settingKey = "exam-types" })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await client.PostAsJsonAsync(Base + "/visit", new { settingKey = "unknown" })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await SendAsync(client, HttpMethod.Post, Base + "/visit", new { settingKey = "subjects" })).GetProperty("firstVisit").GetBoolean().ShouldBeTrue();
		using var denied = As(school, "Academics.Dashboard.View");
		var status = await ReadAsync(denied, Base + "/visit-status");
		status.GetProperty("canAccessSetup").GetBoolean().ShouldBeFalse();
		status.GetProperty("firstVisitedAt").ValueKind.ShouldBe(JsonValueKind.Null);
		(await denied.PostAsJsonAsync(Base + "/visit", new { })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task Identity_and_tenant_boundaries_are_checked_on_every_visit_endpoint()
	{
		var school = await SeedAsync(); var foreign = await SeedAsync();
		using var forged = As(school with { ActorId = foreign.ActorId });
		(await forged.GetAsync(Base + "/visit-status")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await forged.PostAsJsonAsync(Base + "/visit", new { })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		await InAsync(school.TenantId, async services => {
			var repo = services.GetRequiredService<IIdentityUserRepository>(); var actor = await repo.GetAsync(school.ActorId);
			actor.SetIsActive(false); await repo.UpdateAsync(actor, true);
		});
		using var inactive = As(school);
		(await inactive.GetAsync(Base + "/visit-status")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		using var host = factory.CreateClient(); host.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, "host");
		(await host.PostAsJsonAsync(Base + "/visit", new { })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		using var anonymous = factory.CreateClient(); anonymous.DefaultRequestHeaders.Add("X-Skip-Auth", "true");
		(await anonymous.GetAsync(Base + "/visit-status")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Progress_is_year_scoped_concurrent_and_preserves_unrelated_properties()
	{
		var school = await SeedAsync(); var otherYear = await AddYearAsync(school); using var client = As(school);
		var initial = await ReadAsync(client, StateUrl(school));
		var saved = await SendAsync(client, HttpMethod.Put, StateUrl(school) + "/progress",
			Progress(initial, "curriculum", "subjects", ["elective-groups"]));
		saved.GetProperty("currentSettingKey").GetString().ShouldBe("subjects");
		saved.GetProperty("deferredSettingKeys").EnumerateArray().Single().GetString().ShouldBe("elective-groups");
		(await client.PutAsJsonAsync(StateUrl(school) + "/progress", Progress(initial, "review", null))).StatusCode.ShouldBe(HttpStatusCode.Conflict);
		var review = await SendAsync(client, HttpMethod.Put, StateUrl(school) + "/progress", Progress(saved, "review", null, finish: true));
		review.GetProperty("reviewedAt").ValueKind.ShouldBe(JsonValueKind.String);
		var resumed = await SendAsync(client, HttpMethod.Put, StateUrl(school) + "/progress", Progress(review, "timetable", "lecture-slots"));
		resumed.GetProperty("reviewedAt").ValueKind.ShouldBe(JsonValueKind.Null);
		(await ReadAsync(client, StateUrl(school with { YearId = otherYear }))).GetProperty("currentStageKey").GetString().ShouldBe("welcome");
		await InAsync(school.TenantId, async services => {
			var year = await services.GetRequiredService<IRepository<AcademicYear, Guid>>().GetAsync(school.YearId);
			year.GetProperty<string>("SyntheticSibling").ShouldBe("preserved");
			(await services.GetRequiredService<IRepository<Subject, Guid>>().GetListAsync()).Count.ShouldBe(0);
		});
	}

	[Fact]
	public async Task Status_uses_actual_selected_medium_records_and_hides_unauthorized_settings()
	{
		var school = await SeedAsync(); using var client = As(school, SubjectView);
		var noContext = await ReadAsync(client, StateUrl(school));
		Setting(noContext, "subjects").GetProperty("configurationState").GetString().ShouldBe("context-required");
		var empty = await ReadAsync(client, StateUrl(school) + $"?academicMediumId={school.MediumId}");
		Setting(empty, "subjects").GetProperty("configurationState").GetString().ShouldBe("empty");
		Setting(empty, "exam-types").GetProperty("canView").GetBoolean().ShouldBeFalse();
		Setting(empty, "exam-types").GetProperty("configurationState").GetString().ShouldBe("unavailable");
		empty.GetProperty("settings").GetArrayLength().ShouldBe(9);
		await InAsync(school.TenantId, async services => {
			await services.GetRequiredService<IRepository<Subject, Guid>>().InsertAsync(new Subject(Guid.NewGuid(), school.TenantId, "Mathematics", "#123456", school.MediumId), true);
		});
		var configured = await ReadAsync(client, StateUrl(school) + $"?academicMediumId={school.MediumId}");
		Setting(configured, "subjects").GetProperty("configurationState").GetString().ShouldBe("configured");
		Setting(configured, "subjects").GetProperty("canManage").GetBoolean().ShouldBeFalse();
		(await ReadAsync(client, Base + "/visit-status")).GetProperty("hasVisitedConfiguration").GetBoolean().ShouldBeFalse();
	}

	[Fact]
	public async Task Foreign_context_missing_scope_and_invalid_progress_are_rejected_without_writes()
	{
		var school = await SeedAsync(); var foreign = await SeedAsync(); using var client = As(school);
		(await client.GetAsync(StateUrl(foreign))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
		(await client.GetAsync(StateUrl(school) + $"?academicMediumId={foreign.MediumId}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
		var initial = await ReadAsync(client, StateUrl(school));
		(await client.PutAsJsonAsync(StateUrl(school) + "/progress", Progress(initial, "curriculum", "exam-types"))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await client.PutAsJsonAsync(StateUrl(school) + "/progress", Progress(initial, "welcome", null, finish: true))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var noScope = await SeedAsync(withScope: false); using var restricted = As(noScope);
		(await restricted.GetAsync(StateUrl(noScope))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	private static JsonElement Setting(JsonElement state, string key) => state.GetProperty("settings").EnumerateArray().Single(x => x.GetProperty("key").GetString() == key);
	private static object Progress(JsonElement state, string stage, string? key, string[]? deferred = null, bool finish = false) => new {
		concurrencyStamp = state.GetProperty("concurrencyStamp").GetString(), currentStageKey = stage, currentSettingKey = key,
		deferredSettingKeys = deferred ?? [], finishReview = finish };
	private static string StateUrl(School school) => Base + "/" + school.YearId;
	private static async Task<JsonElement> ReadAsync(HttpClient client, string url) {
		var response = await client.GetAsync(url); response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return await response.Content.ReadFromJsonAsync<JsonElement>();
	}
	private static async Task<JsonElement> SendAsync(HttpClient client, HttpMethod method, string url, object input) {
		var response = await client.SendAsync(new HttpRequestMessage(method, url) { Content = JsonContent.Create(input) });
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync()); return await response.Content.ReadFromJsonAsync<JsonElement>();
	}
	private HttpClient As(School school, string? permissions = null) {
		var client = factory.CreateClient(); client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, school.TenantId.ToString());
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, school.ActorId.ToString());
		if (permissions != null) client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, permissions);
		return client;
	}
	private async Task<School> SeedAsync(bool withScope = true) {
		Guid tenantId = default;
		await InAsync(null, async services => {
			var tenant = await services.GetRequiredService<TenantManager>().CreateAsync($"AcademicGuide-{Guid.NewGuid():N}");
			await services.GetRequiredService<ITenantRepository>().InsertAsync(tenant, true); tenantId = tenant.Id;
		});
		School result = null!;
		await InAsync(tenantId, async services => {
			var actor = new IdentityUser(Guid.NewGuid(), "guide-user", "guide@example.test", tenantId);
			await services.GetRequiredService<IIdentityUserRepository>().InsertAsync(actor, true);
			if (withScope) await services.GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new UserScopeAssignment(Guid.NewGuid(), tenantId, userId: actor.Id), true);
			var year = await services.GetRequiredService<AcademicYearManager>().CreateAsync("Guide year", new(2026, 6, 1), new(2027, 5, 31));
			year.SetProperty("SyntheticSibling", "preserved"); await services.GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(year, true);
			var board = await services.GetRequiredService<EducationBoardManager>().CreateAsync(year.Id, EducationBoardType.CBSE);
			await services.GetRequiredService<IRepository<EducationBoard, Guid>>().InsertAsync(board, true);
			var medium = await services.GetRequiredService<EducationMediumManager>().CreateAsync(board.Id, "English");
			await services.GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(medium, true);
			result = new(tenantId, actor.Id, year.Id, medium.Id);
		}); return result;
	}
	private async Task<Guid> AddYearAsync(School school) {
		Guid id = default; await InAsync(school.TenantId, async services => {
			var year = await services.GetRequiredService<AcademicYearManager>().CreateAsync("Next guide year", new(2027, 6, 1), new(2028, 5, 31));
			await services.GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(year, true); id = year.Id;
		}); return id;
	}
	private async Task InAsync(Guid? tenantId, Func<IServiceProvider, Task> action) {
		using var scope = factory.Services.CreateScope(); using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		await action(scope.ServiceProvider); await unit.CompleteAsync();
	}
}
