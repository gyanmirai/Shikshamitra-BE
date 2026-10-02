using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Students.Attendance;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Students)]
public class AttendanceSettingsAuthorizationApi_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	[Fact, TestTier(TestTraits.Tiers.Critical)]
	public async Task Settings_and_schedule_actions_require_exact_existing_permissions()
	{
		using var none = Permissions();
		(await none.GetAsync("/api/students/attendance/settings")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await none.GetAsync($"/api/students/attendance/schedules?enrollmentId={Guid.NewGuid()}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		using var view = Permissions(ShikshaMitraSubmodulePermissions.Students.Attendance_SettingLeaveType.View);
		(await view.PutAsJsonAsync("/api/students/attendance/settings/draft", new SaveAttendanceSettingsDto(), JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await view.PostAsJsonAsync("/api/students/attendance/settings/publish", new PublishAttendanceSettingsDto { ConcurrencyStamp = "invalid", EffectiveFrom = DateTime.Today }, JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		using var add = Permissions(ShikshaMitraSubmodulePermissions.Students.Attendance_MarkAttendance.Add);
		(await add.DeleteAsync($"/api/students/attendance/schedules/{Guid.NewGuid()}?concurrencyStamp=invalid")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}
	[Fact, TestTier(TestTraits.Tiers.Important)]
	public async Task Policy_read_returns_persisted_draft_and_explicit_effective_version()
	{
		var result = await Client.GetAsync($"/api/students/attendance/settings?academicYearId={Factory.SeededAcademicYearId}");
		result.StatusCode.ShouldBe(HttpStatusCode.OK, await result.Content.ReadAsStringAsync());
		var dto = await result.Content.ReadFromJsonAsync<AttendanceSettingsDto>(JsonOptions);
		dto.ShouldNotBeNull(); dto.DraftPolicy.ShouldNotBeNull(); dto.EffectivePolicy.ShouldNotBeNull(); dto.Versions.ShouldNotBeNull();
	}
	[Fact, TestTier(TestTraits.Tiers.Critical)]
	public async Task Draft_publish_reload_and_stale_revision_keep_effective_history_immutable()
	{
		var today = DateTime.UtcNow.AddHours(5.5).Date; Guid yearId;
		using (var services = Factory.Services.CreateScope())
		using (services.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = services.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var year = await services.ServiceProvider.GetRequiredService<AcademicYearManager>().CreateAsync($"AttendancePolicy-{Guid.NewGuid():N}", today.AddDays(-90), today.AddDays(365));
			await services.ServiceProvider.GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(year); await uow.CompleteAsync(); yearId = year.Id;
		}
		var before = await (await Client.GetAsync($"/api/students/attendance/settings?academicYearId={yearId}")).Content.ReadFromJsonAsync<AttendanceSettingsDto>(JsonOptions);
		var policy = before!.DraftPolicy; policy.Defaults.HalfDayCredit = .25m;
		var saved = await Client.PutAsJsonAsync("/api/students/attendance/settings/draft", new SaveAttendanceSettingsDto { AcademicYearId = yearId, ConcurrencyStamp = before.ConcurrencyStamp, Policy = policy }, JsonOptions);
		saved.StatusCode.ShouldBe(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
		var draft = (await saved.Content.ReadFromJsonAsync<AttendanceSettingsDto>(JsonOptions))!;
		var preview = await Client.PostAsJsonAsync("/api/students/attendance/settings/preview", new SaveAttendanceSettingsDto { AcademicYearId = yearId, ConcurrencyStamp = draft.ConcurrencyStamp, Policy = policy }, JsonOptions);
		preview.StatusCode.ShouldBe(HttpStatusCode.OK);
		var published = await Client.PostAsJsonAsync("/api/students/attendance/settings/publish", new PublishAttendanceSettingsDto { AcademicYearId = yearId, ConcurrencyStamp = draft.ConcurrencyStamp!, EffectiveFrom = today }, JsonOptions);
		published.StatusCode.ShouldBe(HttpStatusCode.OK, await published.Content.ReadAsStringAsync());
		var effective = (await published.Content.ReadFromJsonAsync<AttendanceSettingsDto>(JsonOptions))!;
		effective.PublishedVersionId.ShouldNotBeNull(); effective.EffectivePolicy.Defaults.HalfDayCredit.ShouldBe(.25m);
		policy.Defaults.HalfDayCredit = .75m;
		var stale = await Client.PutAsJsonAsync("/api/students/attendance/settings/draft", new SaveAttendanceSettingsDto { AcademicYearId = yearId, ConcurrencyStamp = draft.ConcurrencyStamp, Policy = policy }, JsonOptions);
		stale.IsSuccessStatusCode.ShouldBeFalse();
		var revised = await Client.PutAsJsonAsync("/api/students/attendance/settings/draft", new SaveAttendanceSettingsDto { AcademicYearId = yearId, ConcurrencyStamp = effective.ConcurrencyStamp, Policy = policy }, JsonOptions);
		revised.StatusCode.ShouldBe(HttpStatusCode.OK, await revised.Content.ReadAsStringAsync());
		var reload = (await (await Client.GetAsync($"/api/students/attendance/settings?academicYearId={yearId}")).Content.ReadFromJsonAsync<AttendanceSettingsDto>(JsonOptions))!;
		reload.DraftPolicy.Defaults.HalfDayCredit.ShouldBe(.75m); reload.EffectivePolicy.Defaults.HalfDayCredit.ShouldBe(.25m);
		reload.PublishedVersionId.ShouldBe(effective.PublishedVersionId);
	}
	private HttpClient Permissions(params string[] permissions)
	{
		var client = Factory.CreateClient(); client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
			permissions.Length == 0 ? "__none__" : string.Join(",", permissions)); return client;
	}
}
