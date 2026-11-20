using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Reservations;
using ShikshaMitra.Library.Shared;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryStaffRecordedScope_Tests
{
	[Theory]
	[InlineData(false, 0, 1)]
	[InlineData(true, 0, 1)]
	[InlineData(true, 1, 0)]
	public async Task H_Operational_Dashboard_Should_Use_Actual_Request_States_And_Only_The_Live_Holds_Deadline(
		bool ready, int holdDayOffset, int expiringToday)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		var waitingBook = await SeedOperationalBookAsync(fixture, "Other waiting request", fixture.Context.Catalog.YearId, false);
		await WithUnitOfWorkAsync(async () =>
		{
			var context = fixture.Context;
			var tenantId = GetRequiredService<ICurrentTenant>().Id;
			var requests = GetRequiredService<IRepository<BookReservation, Guid>>();
			var holds = GetRequiredService<IRepository<LibraryHold, Guid>>();
			long sequence = 0;
			foreach (var state in new[] { LibraryReservationState.Cancelled, LibraryReservationState.Expired, LibraryReservationState.Fulfilled })
			{
				var terminal = NewOperationalRequest(fixture, context.Catalog.BookId, ++sequence, new(2026, 11, 18), rawFutureExpiry: true);
				terminal.Ready();
				terminal.Finish(state, "Actual terminal reservation evidence", new(2026, 11, 17, 9, 0, 0, TimeSpan.Zero));
				var hold = NewOperationalHold(fixture, terminal.Id, new(2026, 11, 18));
				Guid? collectedLoanId = null;
				if (state == LibraryReservationState.Fulfilled)
				{
					var copy = await GetRequiredService<IRepository<BookCopy, Guid>>().GetAsync(context.Catalog.CopyId);
					var loan = OperationalCivilLoan(fixture, copy, new(2026, 11, 16), new(2026, 11, 18));
					loan.ReceivePhysicalCopy(new(2026, 11, 17), reviewedUnknownDates: false);
					await GetRequiredService<IRepository<BookIssue, Guid>>().InsertAsync(loan, true);
					collectedLoanId = loan.Id;
				}
				hold.Finish(state == LibraryReservationState.Fulfilled ? LibraryHoldState.Collected :
					state == LibraryReservationState.Expired ? LibraryHoldState.Expired : LibraryHoldState.Released,
					"Actual terminal hold evidence", new(2026, 11, 17, 9, 0, 0, TimeSpan.Zero), collectedLoanId);
				await requests.InsertAsync(terminal, true);
				await holds.InsertAsync(hold, true);
			}
			await requests.InsertAsync(NewOperationalRequest(fixture, waitingBook.Book.Id, ++sequence, new(2026, 11, 30)), true);
			var active = NewOperationalRequest(fixture, context.Catalog.BookId, ++sequence, new(2026, 11, 18));
			active.RequestedDate.ShouldBeNull(); active.ReserveDueDate.ShouldBeNull();
			if (ready) active.Ready();
			await requests.InsertAsync(active, true);
			if (ready)
			{
				await holds.InsertAsync(NewOperationalHold(fixture, active.Id, new DateOnly(2026, 11, 18).AddDays(holdDayOffset)), true);
				var copy = await GetRequiredService<IRepository<BookCopy, Guid>>().GetAsync(context.Catalog.CopyId);
				copy.Status = BookCopyStatus.Reserved;
				await GetRequiredService<IRepository<BookCopy, Guid>>().UpdateAsync(copy, true);
			}
			await requests.InsertAsync(new(Guid.NewGuid(), context.Catalog.BookId, context.StudentId,
				new DateTime(2001, 1, 1), new DateTime(2100, 1, 1), tenantId), true);
		});

		var overview = await ReadOperationalDashboardAsync(null);

		overview.Reservations.ActiveReservations.ShouldBe(2);
		overview.Reservations.ExpiredReservations.ShouldBe(1);
		overview.Reservations.ExpiringToday.ShouldBe(expiringToday);
	}

	private BookReservation NewOperationalRequest(StaffScopeFixture fixture, Guid bookId, long sequence, DateOnly expiry,
		bool rawFutureExpiry = false)
	{
		var context = fixture.Context;
		return BookReservation.Create(Guid.NewGuid(), GetRequiredService<ICurrentTenant>().Id, bookId, context.StudentId,
			context.StudentId, 0, context.Catalog.YearId, sequence, new(2026, 11, 10), expiry, "{}",
			rawExpiryDate: rawFutureExpiry ? new DateTime(2100, 1, 1) : null,
			borrowerContext: new(context.BoardId, context.MediumId, context.StageId, context.BorrowerClassId, null, "{}"));
	}

	private LibraryHold NewOperationalHold(StaffScopeFixture fixture, Guid requestId, DateOnly deadline) =>
		new(Guid.NewGuid(), GetRequiredService<ICurrentTenant>().Id, requestId, fixture.Context.Catalog.BookId,
			fixture.Context.Catalog.CopyId, new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero), deadline,
			LibraryCivilDateProvenance.ExplicitSchoolDate, "UTC", "{}", "{}");
}
