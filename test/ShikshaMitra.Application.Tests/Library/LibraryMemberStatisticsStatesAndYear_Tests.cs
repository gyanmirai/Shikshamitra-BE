using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Dashboard;
using ShikshaMitra.Library.Reservations;
using ShikshaMitra.Library.Shared;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryStaffRecordedScope_Tests
{
	[Fact]
	public async Task H_Statistics_Dashboard_Selected_Year_Should_Use_The_Loan_Year_Of_Permanent_Old_Stock()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		var loan = await CheckoutStatisticsMemberAsync(fixture, staff: false);
		loan.AcademicYearId.ShouldBe(fixture.Context.Catalog.YearId);
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<Book, Guid>>().GetAsync(fixture.Context.Catalog.BookId)))
			.AcademicYearId.ShouldBe(fixture.Context.Catalog.OriginYearId);

		var overview = await GetRequiredService<ILibraryDashboardAppService>().GetOverviewAsync(fixture.Context.Catalog.YearId);

		overview.StockTotals.TotalCopies.ShouldBe(1);
		overview.Circulation.ActiveIssues.ShouldBe(1);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task H_Statistics_Should_Count_Only_Active_Requests_And_Exclude_Terminal_Holds_And_Unreviewed_Legacy_Requests(bool ready)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		await WithUnitOfWorkAsync(async () =>
		{
			var context = fixture.Context;
			var tenantId = GetRequiredService<ICurrentTenant>().Id;
			var requests = GetRequiredService<IRepository<BookReservation, Guid>>();
			var holds = GetRequiredService<IRepository<LibraryHold, Guid>>();
			var copy = await GetRequiredService<IRepository<BookCopy, Guid>>().GetAsync(context.Catalog.CopyId);
			long sequence = 0;
			foreach (var terminal in new[] { LibraryReservationState.Cancelled, LibraryReservationState.Expired, LibraryReservationState.Fulfilled })
			{
				var request = NewStatisticsRequest(fixture, ++sequence);
				request.Ready();
				var hold = NewStatisticsHold(fixture, request.Id, new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero));
				Guid? loanId = null;
				if (terminal == LibraryReservationState.Fulfilled)
				{
					var loan = BookIssue.CreateCivil(Guid.NewGuid(), tenantId, copy.Id, copy.AccessionNumber, context.StudentId, 0,
						new(2026, 11, 10), new(2026, 11, 14), new(context.Catalog.YearId, context.BoardId, context.MediumId,
							context.StageId, context.BorrowerClassId, null, "{}", "{}", "{}", GetRequiredService<ICurrentUser>().Id!.Value,
							new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero)));
					loan.ReceivePhysicalCopy(new(2026, 11, 15), reviewedUnknownDates: false);
					await GetRequiredService<IRepository<BookIssue, Guid>>().InsertAsync(loan, true);
					loanId = loan.Id;
				}
				request.Finish(terminal, "Recorded completed reservation history.", new(2026, 11, 15, 9, 0, 0, TimeSpan.Zero));
				hold.Finish(terminal == LibraryReservationState.Fulfilled ? LibraryHoldState.Collected :
					terminal == LibraryReservationState.Expired ? LibraryHoldState.Expired : LibraryHoldState.Released,
					"Recorded terminal hold history.", new(2026, 11, 15, 9, 0, 0, TimeSpan.Zero), loanId);
				await requests.InsertAsync(request, true);
				await holds.InsertAsync(hold, true);
			}
			var active = NewStatisticsRequest(fixture, ++sequence);
			if (ready) active.Ready();
			await requests.InsertAsync(active, true);
			if (ready) await holds.InsertAsync(NewStatisticsHold(fixture, active.Id, new(2026, 11, 17, 9, 0, 0, TimeSpan.Zero)), true);
			copy.Status = ready ? BookCopyStatus.Reserved : BookCopyStatus.UnderRepair;
			await GetRequiredService<IRepository<BookCopy, Guid>>().UpdateAsync(copy, true);
			await requests.InsertAsync(new(Guid.NewGuid(), context.Catalog.BookId, context.StudentId,
				new DateTime(2001, 1, 1), new DateTime(2001, 1, 2), tenantId), true);
		});

		var stats = await ReadStatisticsAsync(fixture.Context.StudentId, "UTC", new(2026, 11, 18, 0, 30, 0, DateTimeKind.Utc));

		stats.CurrentIssue.ShouldBe(0);
		stats.ReservedBooks.ShouldBe(1);
	}

	private BookReservation NewStatisticsRequest(StaffScopeFixture fixture, long sequence)
	{
		var context = fixture.Context;
		return BookReservation.Create(Guid.NewGuid(), GetRequiredService<ICurrentTenant>().Id, context.Catalog.BookId,
			context.StudentId, context.StudentId, 0, context.Catalog.YearId, sequence, new(2026, 11, 10), new(2026, 11, 30), "{}",
			borrowerContext: new(context.BoardId, context.MediumId, context.StageId, context.BorrowerClassId, null, "{}"));
	}

	private LibraryHold NewStatisticsHold(StaffScopeFixture fixture, Guid requestId, DateTimeOffset at) =>
		new(Guid.NewGuid(), GetRequiredService<ICurrentTenant>().Id, requestId, fixture.Context.Catalog.BookId,
			fixture.Context.Catalog.CopyId, at, new(2026, 11, 30), LibraryCivilDateProvenance.ExplicitSchoolDate, "UTC", "{}", "{}");
}
