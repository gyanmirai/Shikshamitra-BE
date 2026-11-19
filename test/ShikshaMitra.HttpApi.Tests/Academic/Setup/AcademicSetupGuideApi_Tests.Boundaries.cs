using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Academic.Setup;

public partial class AcademicSetupGuideApi_Tests
{
	[Fact]
	public async Task Two_visitors_with_unvisited_snapshots_receive_one_immutable_winner()
	{
		var school = await SeedAsync(); using var first = As(school); using var second = As(school);
		(await ReadAsync(first, Base + "/visit-status")).GetProperty("hasVisitedConfiguration").GetBoolean().ShouldBeFalse();
		(await ReadAsync(second, Base + "/visit-status")).GetProperty("hasVisitedConfiguration").GetBoolean().ShouldBeFalse();
		var winner = await SendAsync(first, HttpMethod.Post, Base + "/visit", new { settingKey = "subjects" });
		var loser = await SendAsync(second, HttpMethod.Post, Base + "/visit", new { settingKey = "lecture-slots" });
		winner.GetProperty("firstVisit").GetBoolean().ShouldBeTrue(); loser.GetProperty("firstVisit").GetBoolean().ShouldBeFalse();
		await InAsync(null, async services => {
			var tenant = await services.GetRequiredService<ITenantRepository>().GetAsync(school.TenantId);
			var marker = AcademicSetupVisit.Read(tenant)!;
			marker.ActorId.ShouldBe(school.ActorId);
			marker.VisitedAt.ShouldBe(DateTime.Parse(winner.GetProperty("firstVisitedAt").GetString()!, null, System.Globalization.DateTimeStyles.RoundtripKind));
		});
		var anotherYear = await AddYearAsync(school);
		(await ReadAsync(first, StateUrl(school with { YearId = anotherYear }))).GetProperty("currentStageKey").GetString().ShouldBe("welcome");
		(await ReadAsync(first, Base + "/visit-status")).GetProperty("hasVisitedConfiguration").GetBoolean().ShouldBeTrue();
		var foreign = await SeedAsync(); using var otherSchool = As(foreign);
		(await ReadAsync(otherSchool, Base + "/visit-status")).GetProperty("hasVisitedConfiguration").GetBoolean().ShouldBeFalse();
	}

	[Theory]
	[InlineData("{\"Version\":2}")]
	[InlineData("{\"Version\":1}")]
	public async Task Corrupt_stored_marker_or_progress_fails_visibly_without_resetting_it(string progressJson)
	{
		var school = await SeedAsync(); using var client = As(school);
		await InAsync(null, async services => {
			var tenants = services.GetRequiredService<ITenantRepository>(); var tenant = await tenants.GetAsync(school.TenantId);
			tenant.SetProperty(AcademicSetupVisit.Property, "{unknown-json"); await tenants.UpdateAsync(tenant, true);
		});
		(await client.GetAsync(Base + "/visit-status")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await client.PostAsJsonAsync(Base + "/visit", new { })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		await InAsync(school.TenantId, async services => {
			var years = services.GetRequiredService<IRepository<AcademicYear, Guid>>(); var year = await years.GetAsync(school.YearId);
			year.SetProperty(AcademicSetupProgress.Property, progressJson); await years.UpdateAsync(year, true);
		});
		(await client.GetAsync(StateUrl(school))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		await InAsync(null, async services => {
			(await services.GetRequiredService<ITenantRepository>().GetAsync(school.TenantId)).GetProperty<string>(AcademicSetupVisit.Property).ShouldBe("{unknown-json");
		});
	}

	[Fact]
	public async Task Visit_collision_reloads_a_fresh_unit_of_work_and_preserves_the_winner()
	{
		var school = await SeedAsync();
		using var scope = factory.Services.CreateScope(); var services = scope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(school.TenantId);
		using var principal = services.GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity([
			new Claim(AbpClaimTypes.UserId, school.ActorId.ToString()), new Claim(AbpClaimTypes.TenantId, school.TenantId.ToString())], "test")));
		var manager = services.GetRequiredService<TenantManager>();
		var stale = await manager.CreateAsync("Synthetic stale school"); var winner = await manager.CreateAsync("Synthetic winning school");
		typeof(Tenant).GetProperty(nameof(Tenant.Id))!.SetValue(stale, school.TenantId);
		typeof(Tenant).GetProperty(nameof(Tenant.Id))!.SetValue(winner, school.TenantId);
		var winningActor = Guid.NewGuid(); var winningTime = new DateTime(2026, 10, 1, 1, 0, 0, DateTimeKind.Utc);
		AcademicSetupVisit.Record(winner, winningActor, winningTime);
		var units = services.GetRequiredService<IUnitOfWorkManager>(); var storage = Substitute.For<ITenantRepository>();
		Guid? firstUnit = null;
		storage.GetAsync(school.TenantId, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(_ => {
			if (firstUnit == null) { firstUnit = units.Current!.Id; return Task.FromResult(stale); }
			units.Current!.Id.ShouldNotBe(firstUnit.Value); return Task.FromResult(winner);
		});
		storage.UpdateAsync(Arg.Any<Tenant>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
			.Returns(_ => Task.FromException<Tenant>(new AbpDbConcurrencyException()));
		var service = new AcademicSetupVisitService(services.GetRequiredService<AcademicSetupAccess>(), storage, units) {
			LazyServiceProvider = services.GetRequiredService<IAbpLazyServiceProvider>() };
		var result = await service.RecordAsync(new() { SettingKey = "subjects" });
		result.FirstVisit.ShouldBeFalse(); result.FirstVisitedAt.ShouldBe(winningTime);
		AcademicSetupVisit.Read(winner)!.ActorId.ShouldBe(winningActor);
	}

	[Fact]
	public async Task Archived_subjects_do_not_count_as_configured_and_year_context_cannot_be_substituted()
	{
		var school = await SeedAsync(); using var client = As(school);
		await InAsync(school.TenantId, async services => {
			await services.GetRequiredService<IRepository<Subject, Guid>>().InsertAsync(new Subject(Guid.NewGuid(), school.TenantId,
				"Archived mathematics", "#123456", school.MediumId, status: SubjectMasterStatus.Archived), true);
		});
		var status = await ReadAsync(client, StateUrl(school) + $"?academicMediumId={school.MediumId}");
		Setting(status, "subjects").GetProperty("configurationState").GetString().ShouldBe("empty");
		var nextYear = await AddYearAsync(school);
		(await client.GetAsync(StateUrl(school with { YearId = nextYear }) + $"?academicMediumId={school.MediumId}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task Partial_permission_progress_preserves_hidden_deferred_settings()
	{
		var school = await SeedAsync(); using var admin = As(school);
		var initial = await ReadAsync(admin, StateUrl(school));
		await SendAsync(admin, HttpMethod.Put, StateUrl(school) + "/progress", Progress(initial, "review", null, ["exam-types"]));
		using var partial = As(school, SubjectView);
		var current = await ReadAsync(partial, StateUrl(school)); current.GetProperty("deferredSettingKeys").GetArrayLength().ShouldBe(0);
		await SendAsync(partial, HttpMethod.Put, StateUrl(school) + "/progress", Progress(current, "curriculum", "subjects", ["subjects"]));
		var visible = await ReadAsync(admin, StateUrl(school));
		visible.GetProperty("deferredSettingKeys").EnumerateArray().Select(x => x.GetString()).ShouldBe(["exam-types", "subjects"]);
	}

	[Fact]
	public async Task Direct_application_service_calls_require_authorized_school_identity()
	{
		using var scope = factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(null);
		var service = scope.ServiceProvider.GetRequiredService<IAcademicSetupAppService>();
		await Should.ThrowAsync<AbpAuthorizationException>(() => service.GetVisitStatusAsync());
		await Should.ThrowAsync<AbpAuthorizationException>(() => service.RecordVisitAsync(new()));
		await Should.ThrowAsync<AbpAuthorizationException>(() => service.GetAsync(Guid.NewGuid(), new()));
		await Should.ThrowAsync<AbpAuthorizationException>(() => service.SaveProgressAsync(Guid.NewGuid(), new() { ConcurrencyStamp = "synthetic" }));
	}
}
