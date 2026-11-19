using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.HR.Policies;

public partial class HrSetupReviewApi_Tests
{
	[Theory]
	[InlineData("calendar"), InlineData("attendance"), InlineData("leave"), InlineData("payroll"), InlineData("sample")]
	public async Task Recording_advisory_review_preserves_succeeded_preview_and_operational_revision(string section)
	{
		var school = await SeedAsync(withStaff: true); using var client = As(school);
		var snapshot = await GetAsync(client, school, section);
		var preview = await SeedReviewPreviewAsync(school);
		(await PreviewStatusAsync(client, preview.Id)).IsCurrent.ShouldBeTrue();
		await AssertPreviewGuardAsync(school, preview.Id, current: true);
		(await client.PostAsJsonAsync(Url(school.YearId, section), Record(snapshot))).EnsureSuccessStatusCode();
		(await GetAsync(client, school, section)).ReviewState.ShouldBe("reviewed");
		(await PayrollRevisionAsync(school)).ShouldBe(preview.Revision);
		(await PreviewStatusAsync(client, preview.Id)).IsCurrent.ShouldBeTrue();
		await AssertPreviewGuardAsync(school, preview.Id, current: true);
	}

	[Theory]
	[InlineData("review", false), InlineData("review", true)]
	[InlineData("dates", false), InlineData("dates", true)]
	[InlineData("extra", false), InlineData("extra", true)]
	[InlineData("policy", false), InlineData("policy", true)]
	public async Task Review_only_saves_are_advisory_but_mixed_real_input_changes_still_invalidate(string change, bool sync)
	{
		var school = await SeedAsync(withStaff: true); using var client = As(school);
		var snapshot = await GetAsync(client, school, "calendar");
		var preview = await SeedReviewPreviewAsync(school);
		await InAsync(school.TenantId, async services => {
			var years = services.GetRequiredService<IRepository<AcademicYear, Guid>>();
			var year = await years.GetAsync(school.YearId);
			AcademicYearHrSetupReview.Record(year, "calendar", new(school.ActorId, DateTimeOffset.UtcNow, snapshot.Fingerprint, null, null));
			if (change == "dates") await services.GetRequiredService<AcademicYearManager>().UpdateAsync(year, year.Name, year.StartDate.AddDays(1), year.EndDate);
			if (change == "extra") year.SetProperty("SyntheticOperationalSetting", true);
			await years.UpdateAsync(year, autoSave: false);
			if (change == "policy") {
				var calendars = services.GetRequiredService<IRepository<WorkCalendarPolicy, Guid>>();
				var calendar = await calendars.GetAsync(school.CalendarId);
				calendar.Update("Changed operational calendar", calendar.Status, calendar.EffectiveFrom, calendar.EffectiveTo,
					calendar.WeeklyOffPattern, calendar.GetCustomWeeklyOffs(), calendar.HolidaySyncMode);
				await calendars.UpdateAsync(calendar, autoSave: false);
			}
			var db = await services.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			if (sync) db.SaveChanges(); else await db.SaveChangesAsync();
		});
		var unchanged = change == "review";
		((await PayrollRevisionAsync(school)) == preview.Revision).ShouldBe(unchanged);
		(await PreviewStatusAsync(client, preview.Id)).IsCurrent.ShouldBe(unchanged);
		await AssertPreviewGuardAsync(school, preview.Id, unchanged);
	}

	private async Task<(Guid Id, Guid Revision)> SeedReviewPreviewAsync(School school)
	{
		var id = Guid.NewGuid(); var revision = Guid.Empty;
		await InAsync(school.TenantId, async services => {
			revision = await services.GetRequiredService<IPayrollInputRevisionStore>().ReadAsync();
			var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(school.ActorId.ToString())));
			var run = new PayrollPreviewRun(id, school.TenantId, school.ActorId, "2026-06", school.YearId, fingerprint, 1, DateTime.UtcNow);
			run.Start(revision, DateTime.UtcNow); run.Complete("{}", DateTime.UtcNow);
			await services.GetRequiredService<IRepository<PayrollPreviewRun, Guid>>().InsertAsync(run, true);
			await services.GetRequiredService<IRepository<PayrollPreviewEmployee, Guid>>().InsertAsync(
				new PayrollPreviewEmployee(Guid.NewGuid(), school.TenantId, id, school.ActorId, "Synthetic reviewer", "SYNTHETIC-REVIEW", "{}"), true);
		});
		return (id, revision);
	}
	private async Task<Guid> PayrollRevisionAsync(School school)
	{
		var revision = Guid.Empty;
		await InAsync(school.TenantId, async services => revision = await services.GetRequiredService<IPayrollInputRevisionStore>().ReadAsync());
		return revision;
	}
	private static async Task<PayrollPreviewStatusDto> PreviewStatusAsync(HttpClient client, Guid id) =>
		(await client.GetFromJsonAsync<PayrollPreviewStatusDto>($"/api/payroll/run/previews/{id}"))!;
	private Task AssertPreviewGuardAsync(School school, Guid id, bool current) => InAsync(school.TenantId, async services => {
		using var actor = services.GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity([
			new Claim(AbpClaimTypes.UserId, school.ActorId.ToString()), new Claim(AbpClaimTypes.TenantId, school.TenantId.ToString())], "SyntheticReview")));
		var guard = services.GetRequiredService<PayrollPreviewReviewGuard>();
		if (current) await guard.EnsureCurrentAsync(id, "2026-06", school.YearId, [school.ActorId]);
		else (await Should.ThrowAsync<BusinessException>(() => guard.EnsureCurrentAsync(id, "2026-06", school.YearId, [school.ActorId])))
			.Code.ShouldBe(HRErrorCodes.PayrollPreviewStale);
	});
}
