using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Finance.Setup;

public partial class FinanceSetupGuideApi_Tests
{
	[Fact]
	public async Task Presence_uses_active_records_in_selected_year_and_accepts_None_categories()
	{
		var school = await SeedAsync(); var nextYear = await AddYearAsync(school);
		await InAsync(school.TenantId, async services => {
			var categories = services.GetRequiredService<IRepository<TransactionCategory, Guid>>();
			await categories.DeleteAsync(x => x.AcademicYearId == school.YearId && x.CategoryType == TransactionType.Income, true);
			await categories.InsertAsync(new TransactionCategory(Guid.NewGuid(), school.TenantId, school.YearId, TransactionType.Income, "Inactive", null, isActive: false), true);
			await categories.InsertAsync(new TransactionCategory(Guid.NewGuid(), school.TenantId, nextYear, TransactionType.Income, "Other year", null), true);
			// Keep the existing persisted active expense None category; it is a valid intentional choice.
			var fees = services.GetRequiredService<IRepository<FeeCategory, Guid>>();
			var inactive = new FeeCategory(Guid.NewGuid(), school.TenantId, school.YearId, "Inactive fees", null);
			inactive.SetStatus(false, "fixture archived"); await fees.InsertAsync(inactive, true);
		});
		using var client = As(school);
		var state = await ReadAsync(client, StateUrl(school));
		Setting(state, "income-categories").GetProperty("configurationState").GetString().ShouldBe("empty");
		Setting(state, "expense-categories").GetProperty("configurationState").GetString().ShouldBe("configured");
		Setting(state, "fee-categories").GetProperty("configurationState").GetString().ShouldBe("empty");
		await InAsync(school.TenantId, async services => {
			await services.GetRequiredService<IRepository<TransactionCategory, Guid>>().InsertAsync(new TransactionCategory(Guid.NewGuid(), school.TenantId, school.YearId, TransactionType.Income, "Active", null), true);
		});
		Setting(await ReadAsync(client, StateUrl(school)), "income-categories").GetProperty("configurationState").GetString().ShouldBe("configured");
	}

	[Fact]
	public async Task Unauthorized_settings_never_touch_their_repositories()
	{
		// Null collaborators make any accidental configuration/scope access fail this focused check.
		var reader = new FinanceSetupConfigurationReader(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);
		var states = FinanceSetupKeys.SettingStages.Keys.Select(key => new FinanceSetupSettingStateDto { Key = key }).ToList();
		await reader.ReadAsync(states, Guid.NewGuid());
		states.ShouldAllBe(x => x.ConfigurationState == "unavailable");
	}

	[Fact]
	public async Task A_year_scope_cannot_be_replaced_with_a_sibling_year()
	{
		var school = await SeedAsync(); var sibling = await AddYearAsync(school);
		await InAsync(school.TenantId, async services => {
			var scope = services.GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
			await scope.DeleteAsync(x => x.UserId == school.ActorId, true);
			await scope.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), school.TenantId, userId: school.ActorId, academicYearId: school.YearId), true);
		});
		using var client = As(school);
		var state = await ReadAsync(client, StateUrl(school));
		(await client.GetAsync(StateUrl(school with { YearId = sibling }))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await client.PutAsJsonAsync(StateUrl(school with { YearId = sibling }) + "/progress", Progress(state, "review", null))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task Corrupt_metadata_fails_without_resetting_and_blank_stamp_conflicts()
	{
		var school = await SeedAsync(); using var client = As(school);
		(await client.PutAsJsonAsync(StateUrl(school) + "/progress", new { concurrencyStamp = "wrong", currentStageKey = "review", deferredSettingKeys = Array.Empty<string>() })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
		await InAsync(school.TenantId, async services => {
			var years = services.GetRequiredService<IRepository<AcademicYear, Guid>>(); var year = await years.GetAsync(school.YearId);
			year.SetProperty(FinanceSetupProgress.Property, "{\"Version\":2}"); await years.UpdateAsync(year, true);
		});
		(await client.GetAsync(StateUrl(school))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		await InAsync(school.TenantId, async services => {
			(await services.GetRequiredService<IRepository<AcademicYear, Guid>>().GetAsync(school.YearId)).GetProperty<string>(FinanceSetupProgress.Property).ShouldBe("{\"Version\":2}");
		});
	}
}
