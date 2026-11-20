using System;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.Library.Policies;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.SettingManagement;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryCalendarConfirmation_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Fact]
	public async Task Tenant_Must_Confirm_Its_Own_Timezone_Even_When_It_Equals_Host_Default()
	{
		await WithUnitOfWorkAsync(() => GetRequiredService<ISettingManager>()
			.SetGlobalAsync(LibraryPolicySettingNames.CalendarTimeZone, "America/New_York"));
		var tenantId = Guid.NewGuid();
		using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var principal = GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity([
			new Claim(AbpClaimTypes.UserId, Guid.NewGuid().ToString()), new Claim(AbpClaimTypes.TenantId, tenantId.ToString())
		], "LibraryCalendarTest")));
		var context = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		var calendar = GetRequiredService<ILibraryCalendarAppService>();

		var inherited = await calendar.GetSettingsAsync(context.YearId);
		inherited.NeedsConfiguration.ShouldBeTrue();
		inherited.CanPublishAutomatedPolicy.ShouldBeFalse();

		await calendar.UpdateSettingsAsync(new() { TimeZoneId = "America/New_York" });
		var confirmed = await calendar.GetSettingsAsync(context.YearId);

		confirmed.Source.ShouldBe("Library");
		confirmed.NeedsConfiguration.ShouldBeFalse();
		confirmed.CanPublishAutomatedPolicy.ShouldBeTrue();
	}

	[Fact]
	public void School_Date_Should_Use_Configured_Timezone_Across_Utc_Midnight()
	{
		var instant = new DateTimeOffset(2026, 1, 1, 1, 30, 0, TimeSpan.Zero);

		LibraryCalendarCalculator.SchoolDate(instant, TimeZoneInfo.FindSystemTimeZoneById("America/New_York"))
			.ShouldBe(new DateOnly(2025, 12, 31));
		LibraryCalendarCalculator.SchoolDate(instant, TimeZoneInfo.FindSystemTimeZoneById("Pacific/Auckland"))
			.ShouldBe(new DateOnly(2026, 1, 1));
	}
}
