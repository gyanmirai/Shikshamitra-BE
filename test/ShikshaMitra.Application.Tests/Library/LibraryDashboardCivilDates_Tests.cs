using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Shared;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryStaffRecordedScope_Tests
{
	[Theory]
	[InlineData("UTC", 0, 1, 1)]
	[InlineData("Pacific/Honolulu", 1, 0, 0)]
	public async Task H_Operational_Dashboard_Should_Use_Confirmed_Civil_Dates_And_The_Library_School_Day(
		string zone, int issuedToday, int returnedToday, int overdue)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		await WithUnitOfWorkAsync(async () =>
		{
			var context = fixture.Context;
			var copy = await GetRequiredService<IRepository<BookCopy, Guid>>().GetAsync(context.Catalog.CopyId);
			var returnedCopy = new BookCopy(Guid.NewGuid(), copy.BookId, 2, $"RETURNED-{Guid.NewGuid():N}",
				copy.SectionId, copy.ShelfId, copy.TenantId);
			await GetRequiredService<IRepository<BookCopy, Guid>>().InsertAsync(returnedCopy, true);
			var open = OperationalCivilLoan(fixture, copy, new(2026, 11, 17), new(2026, 11, 17));
			var returned = OperationalCivilLoan(fixture, returnedCopy, new(2026, 11, 16), new(2026, 11, 18));
			returned.ReceivePhysicalCopy(new(2026, 11, 18), reviewedUnknownDates: false);
			open.IssueDate.ShouldBeNull(); open.DueDate.ShouldBeNull();
			returned.ReturnDate.ShouldBeNull();
			open.IssueDateProvenance.ShouldBe(LibraryCivilDateProvenance.ExplicitSchoolDate);
			returned.ReturnDateProvenance.ShouldBe(LibraryCivilDateProvenance.ExplicitSchoolDate);
			await GetRequiredService<IRepository<BookIssue, Guid>>().InsertManyAsync([open, returned], true);
		});

		var overview = await ReadOperationalDashboardAsync(fixture.Context.Catalog.YearId, zone);

		overview.Circulation.ActiveIssues.ShouldBe(1);
		overview.Circulation.IssuesToday.ShouldBe(issuedToday);
		overview.Circulation.ReturnsToday.ShouldBe(returnedToday);
		overview.Circulation.OverdueIssues.ShouldBe(overdue);
		overview.Circulation.IssuesThisWeek.ShouldBe(2);
		overview.Circulation.ReturnsThisWeek.ShouldBe(returnedToday);
		overview.WeeklyCirculation.Sum(point => point.Issued).ShouldBe(2);
		overview.WeeklyCirculation.Sum(point => point.Returned).ShouldBe(returnedToday);
	}

	[Fact]
	public async Task H_Operational_Dashboard_Should_Not_Classify_A_Raw_Unconfirmed_Due_Timestamp_As_Overdue()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		await WithUnitOfWorkAsync(async () =>
		{
			var copy = await GetRequiredService<IRepository<BookCopy, Guid>>().GetAsync(fixture.Context.Catalog.CopyId);
			var loan = new BookIssue(Guid.NewGuid(), copy.Id, copy.AccessionNumber, fixture.Context.StudentId, 0,
				new DateTime(2001, 1, 1), new DateTime(2001, 1, 2), copy.TenantId);
			loan.SchoolDueDate.ShouldBeNull();
			loan.DueDateProvenance.ShouldBe(LibraryCivilDateProvenance.UnconfirmedStoredComponent);
			await GetRequiredService<IRepository<BookIssue, Guid>>().InsertAsync(loan, true);
		});

		var overview = await ReadOperationalDashboardAsync(null);

		overview.Circulation.ActiveIssues.ShouldBe(1);
		overview.Circulation.OverdueIssues.ShouldBe(0);
		overview.WeeklyCirculation.Sum(point => point.Issued).ShouldBe(0);
	}

	[Fact]
	public async Task H_Operational_Dashboard_Should_Not_Credit_A_Conflicting_Accession_To_A_Second_Title()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		var other = await SeedOperationalBookAsync(fixture, "Wrong popularity candidate", fixture.Context.Catalog.YearId, true);
		await SeedOperationalConflictingLoanAsync(fixture, other.Copy!.AccessionNumber);

		var overview = await ReadOperationalDashboardAsync(fixture.Context.Catalog.YearId);

		overview.PopularBooks.Single().BookId.ShouldBe(fixture.Context.Catalog.BookId);
		overview.PopularBooks.Single().IssueCount.ShouldBe(1);
		overview.PopularBooks.Single().ActiveIssues.ShouldBe(1);
	}

	private BookIssue OperationalCivilLoan(StaffScopeFixture fixture, BookCopy copy, DateOnly issue, DateOnly due)
	{
		var context = fixture.Context;
		return BookIssue.CreateCivil(Guid.NewGuid(), copy.TenantId, copy.Id, copy.AccessionNumber, context.StudentId, 0,
			issue, due, new(context.Catalog.YearId, context.BoardId, context.MediumId, context.StageId,
				context.BorrowerClassId, null, "{}", "{}", "{}", GetRequiredService<ICurrentUser>().Id!.Value,
				new(2026, 11, 17, 9, 0, 0, TimeSpan.Zero)));
	}
}
