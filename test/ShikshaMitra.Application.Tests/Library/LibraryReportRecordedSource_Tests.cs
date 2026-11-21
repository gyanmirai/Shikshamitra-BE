using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Reservations;
using ShikshaMitra.Library.Reports;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryStaffRecordedScope_Tests
{
	[Theory]
	[InlineData(false, false, false)]
	[InlineData(false, false, true)]
	[InlineData(false, true, false)]
	[InlineData(false, true, true)]
	[InlineData(true, false, false)]
	[InlineData(true, false, true)]
	[InlineData(true, true, false)]
	[InlineData(true, true, true)]
	public async Task H_Operational_Report_Should_Use_Recorded_Source_Scope_For_Rows_And_Counts(bool staff, bool originalScope, bool reservation)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		var memberId = staff ? fixture.EmployeeId : fixture.Context.StudentId;
		var currentStock = await SeedOperationalBookAsync(fixture, "Current-origin source-scope title", fixture.Context.Catalog.YearId, true);
		fixture = fixture with { Context = fixture.Context with { Catalog = fixture.Context.Catalog with
		{
			BookId = currentStock.Book.Id, CopyId = currentStock.Copy!.Id
		} } };
		var loan = await CheckoutStatisticsMemberAsync(fixture, staff);
		var waitingTitle = await SeedOperationalBookAsync(fixture, "Current-origin request title", fixture.Context.Catalog.YearId, false);
		var request = await WithUnitOfWorkAsync(() => GetRequiredService<LibraryReservationCoordinator>().CreateAsync(waitingTitle.Book.Id,
			memberId, fixture.Context.Catalog.YearId, new(2026, 11, 17), new(2026, 11, 30),
			LibraryOperationActor.User(GetRequiredService<ICurrentUser>().Id!.Value)));
		await MoveStatisticsBorrowerAndActorAsync(fixture, staff, originalScope);
		await WithUnitOfWorkAsync(async () =>
		{
			var scope = await GetRequiredService<IUserScopeAccessService>().GetCurrentUserScopeAsync();
			(await GetRequiredService<LibraryStoredLoanScope>().CanReadAsync(loan.Id, scope)).ShouldBe(originalScope);
			(await GetRequiredService<LibraryStoredReservationScope>().CanReadAsync(request.Id, scope)).ShouldBe(originalScope);
			var currentPeople = await GetRequiredService<ILibraryAccessService>().GetMemberIdsAsync(scope);
			currentPeople.Contains(memberId).ShouldBe(!originalScope);
		});

		var report = await ReadOperationalReportAsync(reservation ? LibraryReportTypes.ReservationList : LibraryReportTypes.IssueReturn,
			fixture.Context.Catalog.YearId);

		report.TotalCount.ShouldBe(originalScope ? 1 : 0);
		report.Rows.Count.ShouldBe(originalScope ? 1 : 0);
		if (originalScope) report.Rows.Single()["title"].ShouldBe(reservation ? waitingTitle.Book.Title : currentStock.Book.Title);
	}

	[Fact]
	public async Task H_Operational_Report_Selected_Year_Should_Use_The_Recorded_Loan_Year_Of_Old_Permanent_Stock()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		var loan = await CheckoutStatisticsMemberAsync(fixture, staff: false);
		loan.AcademicYearId.ShouldBe(fixture.Context.Catalog.YearId);
		var book = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<Book, Guid>>().GetAsync(fixture.Context.Catalog.BookId));
		book.AcademicYearId.ShouldBe(fixture.Context.Catalog.OriginYearId);

		var report = await ReadOperationalReportAsync(LibraryReportTypes.IssueReturn, fixture.Context.Catalog.YearId);

		report.TotalCount.ShouldBe(1);
		report.Rows.Single()["title"].ShouldBe(book.Title);
	}

	[Fact]
	public async Task H_Operational_Report_Should_Resolve_The_Stored_Copy_Id_Before_A_Conflicting_Accession()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		var other = await SeedOperationalBookAsync(fixture, "Wrong accession candidate", fixture.Context.Catalog.YearId, true);
		await SeedOperationalConflictingLoanAsync(fixture, other.Copy!.AccessionNumber);

		var report = await ReadOperationalReportAsync(LibraryReportTypes.IssueReturn);

		report.TotalCount.ShouldBe(1);
		report.Rows.Single()["title"].ShouldBe("Persistent library title");
	}

	private Task SeedOperationalConflictingLoanAsync(StaffScopeFixture fixture, string conflictingAccession) => WithUnitOfWorkAsync(async () =>
	{
		var context = fixture.Context;
		var copy = await GetRequiredService<IRepository<BookCopy, Guid>>().GetAsync(context.Catalog.CopyId);
		var loan = BookIssue.CreateCivil(Guid.NewGuid(), copy.TenantId, copy.Id, conflictingAccession, context.StudentId, 0,
			new(2026, 11, 17), new(2026, 11, 24), new(context.Catalog.YearId, context.BoardId, context.MediumId,
				context.StageId, context.BorrowerClassId, null, "{}", "{}", "{}", GetRequiredService<ICurrentUser>().Id!.Value,
				new(2026, 11, 17, 9, 0, 0, TimeSpan.Zero)));
		await GetRequiredService<IRepository<BookIssue, Guid>>().InsertAsync(loan, true);
	});
}
