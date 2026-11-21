using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Policies;
using ShikshaMitra.Library.Shared;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryStaffRecordedScope_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task H_Statistics_Classification_Should_Keep_Confirmed_Overdue_Subset_And_Expose_Unknown_Due_Evidence(bool unknownDue)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		await SeedStatisticsLoanAsync(fixture, new(2026, 11, 17));
		if (unknownDue) await WithUnitOfWorkAsync(async () =>
		{
			var context = fixture.Context;
			var copy = new BookCopy(Guid.NewGuid(), context.Catalog.BookId, 2, $"RAW-{Guid.NewGuid():N}", context.Catalog.SectionId,
				context.Catalog.ShelfId, GetRequiredService<Volo.Abp.MultiTenancy.ICurrentTenant>().Id);
			copy.Status = BookCopyStatus.Issued;
			await GetRequiredService<IRepository<BookCopy, Guid>>().InsertAsync(copy, true);
			var loan = new BookIssue(Guid.NewGuid(), copy.Id, copy.AccessionNumber, context.StudentId, 0,
				new DateTime(2001, 1, 1), new DateTime(2001, 1, 2), copy.TenantId);
			loan.DueDateProvenance.ShouldBe(LibraryCivilDateProvenance.UnconfirmedStoredComponent);
			await GetRequiredService<IRepository<BookIssue, Guid>>().InsertAsync(loan, true);
		});

		var stats = await ReadStatisticsAsync(fixture.Context.StudentId, "UTC", new(2026, 11, 18, 0, 30, 0, DateTimeKind.Utc));

		stats.CurrentIssue.ShouldBe(unknownDue ? 2 : 1);
		stats.OverdueReturnBooks.ShouldBe(1);
		stats.UnknownDueDateCount.ShouldBe(unknownDue ? 1 : 0);
		stats.OverdueClassificationAvailable.ShouldBe(!unknownDue);
		stats.OverdueAsOfDate.ShouldBe(new DateOnly(2026, 11, 18));
		stats.OverdueDateSource.ShouldBe("ConfirmedLibraryClock");
		if (unknownDue) stats.OverdueUnavailableReason.ShouldNotBeNullOrWhiteSpace();
		else stats.OverdueUnavailableReason.ShouldBeNull();
	}

	[Fact]
	public async Task H_Statistics_Classification_Without_An_Explicit_Library_Clock_Should_Not_Present_Zero_As_Fully_Classified()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		await SeedStatisticsLoanAsync(fixture, new(2026, 11, 17));
		(await WithUnitOfWorkAsync(() => GetRequiredService<LibraryCalendarService>().GetSettingsAsync())).Source.ShouldNotBe("Library");
		var services = new StatisticsClockServices(ServiceProvider, new(2026, 11, 18, 0, 30, 0, DateTimeKind.Utc));

		var stats = (await WithUnitOfWorkAsync(() => services.Get().ReadAsync([fixture.Context.StudentId])))[fixture.Context.StudentId];

		stats.CurrentIssue.ShouldBe(1);
		stats.OverdueReturnBooks.ShouldBe(0);
		stats.UnknownDueDateCount.ShouldBe(0);
		stats.OverdueClassificationAvailable.ShouldBeFalse();
		stats.OverdueAsOfDate.ShouldBeNull();
		stats.OverdueDateSource.ShouldBe("Unavailable");
		stats.OverdueUnavailableReason.ShouldNotBeNullOrWhiteSpace();
	}
}
