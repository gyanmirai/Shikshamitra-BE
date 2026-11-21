using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Library.Policies;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;
using Xunit;

namespace ShikshaMitra.Library;

// Outside the compiled graph. Uses the real typed source and Dashboard-only
// fixture; only the read clock changes, after real posting/collection audits.
public partial class LibraryFinanceExternalReceivable_Tests
{
	[Fact]
	public async Task Dashboard_Overdue_Charge_Alert_Should_Use_The_Confirmed_Library_Day_Across_A_Honolulu_Boundary()
	{
		var fixture = await SeedTypedRefundAsync(FinanceExternalPayerKind.Employee);
		var owner = fixture.Settlement.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		await AssertDashboardClockSourceAsync(owner);
		var retained = await CaptureDashboardClockSourceAsync(owner);
		// 00:30 UTC on the day after the actual confirmed due date remains the
		// due date in Honolulu; this instant never comes from the browser/raw dates.
		var instant = owner.DueDate.AddDays(1).ToDateTime(new TimeOnly(0, 30), DateTimeKind.Utc);
		await SetDashboardClockZoneAsync(owner, LibraryPolicySettingNames.LegacyHrTimeZone, "UTC");
		await SetDashboardClockZoneAsync(owner, LibraryPolicySettingNames.CalendarTimeZone, "Pacific/Honolulu");
		var denied = await RestrictConsumerToDashboardAsync(owner);
		await AssertDashboardConsumerAuthorityAsync(owner, denied);

		var honolulu = await WithUnitOfWorkAsync(() => ConsumerDashboardAtUtc(owner, instant).GetOverviewAsync(owner.YearId));

		honolulu.GeneratedAt.ShouldBe(instant);
		honolulu.SchoolAsOfDate.ShouldBe(owner.DueDate);
		honolulu.SchoolDateSource.ShouldBe("ConfirmedLibraryClock");
		honolulu.SchoolDateUnavailableReason.ShouldBeNull();
		honolulu.Alerts.ShouldNotContain(alert => alert.Type == "overdueCharges");
		AssertDashboardClockMoney(honolulu);
		// Positive classification control: only the explicitly saved Library
		// timezone changes. The same actual due date/payment/source is retained.
		await SetDashboardClockZoneAsync(owner, LibraryPolicySettingNames.CalendarTimeZone, "UTC");
		var utc = await WithUnitOfWorkAsync(() => ConsumerDashboardAtUtc(owner, instant).GetOverviewAsync(owner.YearId));
		utc.SchoolAsOfDate.ShouldBe(owner.DueDate.AddDays(1));
		utc.SchoolDateSource.ShouldBe("ConfirmedLibraryClock");
		utc.Alerts.Where(alert => alert.Type == "overdueCharges").ShouldHaveSingleItem().Count.ShouldBe(1);
		AssertDashboardClockMoney(utc);
		await AssertDashboardClockSourceAsync(owner);
		(await CaptureDashboardClockSourceAsync(owner)).ShouldBe(retained);
	}

	[Fact]
	public async Task Dashboard_Should_Keep_Charge_Overdue_Classification_Unavailable_With_Only_A_Legacy_Hr_Timezone()
	{
		var fixture = await SeedTypedRefundAsync(FinanceExternalPayerKind.Employee);
		var owner = fixture.Settlement.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		await AssertDashboardClockSourceAsync(owner);
		var retained = await CaptureDashboardClockSourceAsync(owner);
		// This is a fresh school, not a reflection-clear or a setting removed from
		// an already configured source. Global inheritance cannot confirm Library.
		await WithUnitOfWorkAsync(async () =>
		{
			var settings = GetRequiredService<ISettingManager>();
			(await settings.GetOrNullForTenantAsync(LibraryPolicySettingNames.CalendarTimeZone,
				owner.TenantId, fallback: false)).ShouldBeNull();
			(await settings.GetOrNullForTenantAsync(LibraryPolicySettingNames.LegacyHrTimeZone,
				owner.TenantId, fallback: false)).ShouldBeNull();
		});
		var instant = owner.DueDate.AddDays(1).ToDateTime(new TimeOnly(0, 30), DateTimeKind.Utc);
		var denied = await RestrictConsumerToDashboardAsync(owner);
		await AssertDashboardConsumerAuthorityAsync(owner, denied);
		var missing = await WithUnitOfWorkAsync(() => ConsumerDashboardAtUtc(owner, instant).GetOverviewAsync(owner.YearId));
		missing.SchoolAsOfDate.ShouldBeNull(); missing.SchoolDateSource.ShouldBe("Unavailable");
		missing.SchoolDateUnavailableReason.ShouldNotBeNullOrWhiteSpace();
		missing.Alerts.ShouldNotContain(alert => alert.Type == "overdueCharges");
		AssertDashboardClockMoney(missing);
		// The HR definition can supply a fallback even before a tenant row exists.
		// Explicit HR configuration still cannot confirm a Library school clock.
		await SetDashboardClockZoneAsync(owner, LibraryPolicySettingNames.LegacyHrTimeZone, "UTC");
		var calendar = DashboardClockServices(owner, instant).GetRequiredService<LibraryCalendarService>();
		var setup = await WithUnitOfWorkAsync(() => calendar.GetSettingsAsync());
		setup.Source.ShouldBe("LegacyHr"); setup.TimeZoneId.ShouldBe("UTC"); setup.NeedsConfiguration.ShouldBeTrue();
		(await WithUnitOfWorkAsync(() => calendar.TodayAsync())).ShouldBe(owner.DueDate.AddDays(1));

		var overview = await WithUnitOfWorkAsync(() => ConsumerDashboardAtUtc(owner, instant).GetOverviewAsync(owner.YearId));

		overview.GeneratedAt.ShouldBe(instant);
		overview.SchoolAsOfDate.ShouldBeNull();
		overview.SchoolDateSource.ShouldBe("Unavailable");
		overview.SchoolDateUnavailableReason.ShouldNotBeNullOrWhiteSpace();
		overview.Alerts.ShouldNotContain(alert => alert.Type == "overdueCharges",
			"An HR fallback is not a confirmed Library school day for a charge due-date claim.");
		// Unknown as-of date does not erase verified settlement evidence.
		AssertDashboardClockMoney(overview);
		await AssertDashboardClockSourceAsync(owner);
		(await CaptureDashboardClockSourceAsync(owner)).ShouldBe(retained);
	}
}

