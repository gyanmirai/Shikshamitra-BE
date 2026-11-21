using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Policies;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryStaffRecordedScope_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task H_ReadEvidence_Dashboard_Should_Expose_Unknown_Dates_Alongside_The_Confirmed_Overdue_Subset(bool unknownDue)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		await SeedStatisticsLoanAsync(fixture, new(2026, 11, 17));
		if (unknownDue)
		{
			var holding = await SeedOperationalBookAsync(fixture, "Unconfirmed holding", fixture.Context.Catalog.OriginYearId, true);
			var copy = holding.Copy!;
			await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<BookIssue, Guid>>().InsertAsync(new(Guid.NewGuid(),
				copy.Id, copy.AccessionNumber, fixture.Context.StudentId, 0, new(2001, 1, 1), new(2001, 1, 2), copy.TenantId), true));
		}

		var overview = await ReadOperationalDashboardAsync(null);

		overview.Circulation.OverdueIssues.ShouldBe(1);
		overview.Circulation.OverdueClassificationAvailable.ShouldBe(!unknownDue);
		overview.Circulation.UnknownDueDateCount.ShouldBe(unknownDue ? 1 : 0);
		overview.Circulation.OverdueAsOfDate.ShouldBe(new DateOnly(2026, 11, 18));
		overview.Circulation.OverdueDateSource.ShouldBe("ConfirmedLibraryClock");
		overview.SchoolAsOfDate.ShouldBe(new DateOnly(2026, 11, 18));
		overview.UnknownIssueDateCount.ShouldBe(unknownDue ? 1 : 0);
		if (unknownDue) overview.Circulation.OverdueUnavailableReason.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task H_ReadEvidence_Dashboard_Without_A_Confirmed_Library_Clock_Should_Expose_Unavailable_Classification()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		await SeedStatisticsLoanAsync(fixture, new(2026, 11, 17));
		var services = new OperationalClockServices(ServiceProvider, new(2026, 11, 18, 0, 30, 0, DateTimeKind.Utc));
		var settings = await WithUnitOfWorkAsync(() => ((LibraryCalendarService)services.GetService(typeof(LibraryCalendarService))!).GetSettingsAsync());
		(settings.Source != "Library" || settings.NeedsConfiguration).ShouldBeTrue();

		var overview = await WithUnitOfWorkAsync(() => services.Dashboard.GetOverviewAsync());

		overview.Circulation.ActiveIssues.ShouldBe(1);
		overview.Circulation.OverdueIssues.ShouldBe(0);
		overview.Circulation.OverdueClassificationAvailable.ShouldBeFalse();
		overview.Circulation.OverdueUnavailableReason.ShouldNotBeNullOrWhiteSpace();
		overview.SchoolAsOfDate.ShouldBeNull();
		overview.SchoolDateSource.ShouldBe("Unavailable");
		overview.SchoolDateUnavailableReason.ShouldNotBeNullOrWhiteSpace();
		overview.WeeklyCirculation.ShouldBeEmpty();
	}
}
