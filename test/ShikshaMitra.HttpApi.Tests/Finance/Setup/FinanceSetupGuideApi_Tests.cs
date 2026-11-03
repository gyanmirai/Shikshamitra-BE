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

namespace ShikshaMitra.Finance.Setup;

[Collection(InitialSetupTestCollection.Name), Trait("Module", "Finance"), Trait("Tier", "Critical")]
public partial class FinanceSetupGuideApi_Tests(InitialSetupTestFactory factory)
{
	private const string Base = "/api/finance/setup";
	private const string IncomeView = "Finance.Accounts.Settings.IncomeCategory.View";
	private sealed record School(Guid TenantId, Guid ActorId, Guid YearId, Guid MediumId);

	[Fact]
	public async Task Progress_is_year_scoped_concurrent_and_review_does_not_require_financial_transactions()
	{
		var school = await SeedAsync(); var otherYear = await AddYearAsync(school); using var client = As(school);
		var initial = await ReadAsync(client, StateUrl(school));
		initial.GetProperty("currentStageKey").GetString().ShouldBe("accounts");
		initial.GetProperty("currentSettingKey").GetString().ShouldBe("income-categories");
		initial.GetProperty("settings").GetArrayLength().ShouldBe(12);
		var saved = await SendAsync(client, HttpMethod.Put, StateUrl(school) + "/progress", Progress(initial, "fees", "payment-terms", ["bank-accounts"]));
		(await client.PutAsJsonAsync(StateUrl(school) + "/progress", Progress(initial, "review", null))).StatusCode.ShouldBe(HttpStatusCode.Conflict);
		var review = await SendAsync(client, HttpMethod.Put, StateUrl(school) + "/progress", Progress(saved, "review", null, finish: true));
		review.GetProperty("reviewedAt").ValueKind.ShouldBe(JsonValueKind.String);
		var resumed = await SendAsync(client, HttpMethod.Put, StateUrl(school) + "/progress", Progress(review, "assignment", null));
		resumed.GetProperty("reviewedAt").ValueKind.ShouldBe(JsonValueKind.Null);
		(await ReadAsync(client, StateUrl(school with { YearId = otherYear }))).GetProperty("currentStageKey").GetString().ShouldBe("accounts");
		await InAsync(school.TenantId, async services => {
			var year = await services.GetRequiredService<IRepository<AcademicYear, Guid>>().GetAsync(school.YearId);
			year.GetProperty<string>("SyntheticSibling").ShouldBe("preserved");
			ShikshaMitra.Academic.Setup.AcademicSetupProgress.Read(year).CurrentStageKey.ShouldBe("welcome");
		});
	}

	[Fact]
	public async Task Exact_view_controls_state_and_manage_requires_an_exact_add_or_edit()
	{
		var school = await SeedAsync(); using var client = As(school, IncomeView);
		var state = await ReadAsync(client, StateUrl(school));
		Setting(state, "income-categories").GetProperty("canView").GetBoolean().ShouldBeTrue();
		Setting(state, "income-categories").GetProperty("canManage").GetBoolean().ShouldBeFalse();
		Setting(state, "bank-accounts").GetProperty("configurationState").GetString().ShouldBe("unavailable");
		using var editor = As(school, IncomeView + ",Finance.Accounts.Settings.IncomeCategory.Edit");
		Setting(await ReadAsync(editor, StateUrl(school)), "income-categories").GetProperty("canManage").GetBoolean().ShouldBeTrue();
		using var addOnly = As(school, "Finance.Accounts.Settings.IncomeCategory.Add");
		(await addOnly.GetAsync(StateUrl(school))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await addOnly.PutAsJsonAsync(StateUrl(school) + "/progress", Progress(state, "review", null))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Theory]
	[InlineData("Finance.Fees.StructureAssign.View")]
	[InlineData("Finance.Fees.FeeCollections.View")]
	[InlineData("Finance.Fees.Charges.View")]
	[InlineData("Finance.Fees.Refund.View")]
	[InlineData("Finance.Fees.Concession.View")]
	[InlineData("Finance.Fees.InvoiceReceipt.View")]
	[InlineData("Finance.Accounts.IncomeTracking.View")]
	[InlineData("Finance.Accounts.ExpenseTracking.View")]
	[InlineData("Finance.Accounts.CashManagement.View")]
	[InlineData("Finance.Accounts.Reconciliation.View")]
	[InlineData("Finance.Auditing.View")]
	public async Task Operational_view_allows_guidance_but_reveals_no_setting_configuration(string permission)
	{
		var school = await SeedAsync(); using var client = As(school, permission);
		var state = await ReadAsync(client, StateUrl(school));
		state.GetProperty("settings").EnumerateArray().ShouldAllBe(x => !x.GetProperty("canView").GetBoolean() && x.GetProperty("configurationState").GetString() == "unavailable");
		await SendAsync(client, HttpMethod.Put, StateUrl(school) + "/progress", Progress(state, "operations", null));
	}

	[Fact]
	public async Task Tampered_keys_hidden_deferred_and_invalid_progress_are_guarded()
	{
		var school = await SeedAsync(); using var admin = As(school);
		var initial = await ReadAsync(admin, StateUrl(school));
		await SendAsync(admin, HttpMethod.Put, StateUrl(school) + "/progress", Progress(initial, "review", null, ["bank-accounts"]));
		using var partial = As(school, IncomeView); var current = await ReadAsync(partial, StateUrl(school));
		current.GetProperty("deferredSettingKeys").GetArrayLength().ShouldBe(0);
		(await partial.PutAsJsonAsync(StateUrl(school) + "/progress", Progress(current, "accounts", "bank-accounts"))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await partial.PutAsJsonAsync(StateUrl(school) + "/progress", Progress(current, "review", null, ["bank-accounts"]))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		await SendAsync(partial, HttpMethod.Put, StateUrl(school) + "/progress", Progress(current, "accounts", "income-categories", ["income-categories"]));
		var visible = await ReadAsync(admin, StateUrl(school));
		visible.GetProperty("deferredSettingKeys").EnumerateArray().Select(x => x.GetString()).ShouldBe(["bank-accounts", "income-categories"]);
		foreach (var bad in new[] { Progress(visible, "accounts", "payment-terms"), Progress(visible, "operations", null, finish: true), Progress(visible, "review", null, ["unknown"]), Progress(visible, "review", null, ["bank-accounts", "bank-accounts"]) })
			(await admin.PutAsJsonAsync(StateUrl(school) + "/progress", bad)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Tenant_year_scope_active_identity_and_action_are_checked_for_read_and_write()
	{
		var school = await SeedAsync(); var foreign = await SeedAsync(); using var client = As(school);
		var state = await ReadAsync(client, StateUrl(school));
		(await client.GetAsync(StateUrl(foreign))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
		(await client.PutAsJsonAsync(StateUrl(foreign) + "/progress", Progress(state, "review", null))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
		var noScope = await SeedAsync(withScope: false); using var restricted = As(noScope);
		(await restricted.GetAsync(StateUrl(noScope))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await restricted.PutAsJsonAsync(StateUrl(noScope) + "/progress", Progress(state, "review", null))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		using var forged = As(school with { ActorId = foreign.ActorId });
		(await forged.GetAsync(StateUrl(school))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await forged.PutAsJsonAsync(StateUrl(school) + "/progress", Progress(state, "review", null))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		await InAsync(school.TenantId, async services => {
			var users = services.GetRequiredService<IIdentityUserRepository>(); var actor = await users.GetAsync(school.ActorId);
			actor.SetIsActive(false); await users.UpdateAsync(actor, true);
		});
		(await client.GetAsync(StateUrl(school))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await client.PutAsJsonAsync(StateUrl(school) + "/progress", Progress(state, "review", null))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		using var anonymous = factory.CreateClient(); anonymous.DefaultRequestHeaders.Add("X-Skip-Auth", "true");
		(await anonymous.GetAsync(StateUrl(school))).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
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
			var tenant = await services.GetRequiredService<TenantManager>().CreateAsync($"FinanceGuide-{Guid.NewGuid():N}");
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
