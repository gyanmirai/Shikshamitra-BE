using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Reservations;
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
	[InlineData(false, false)]
	[InlineData(false, true)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task H_Statistics_Should_Count_Only_Recorded_Loan_And_Request_Scope_After_Reassignment(bool staff, bool originalScope)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		var memberId = staff ? fixture.EmployeeId : fixture.Context.StudentId;
		var loan = await CheckoutStatisticsMemberAsync(fixture, staff);
		var requestId = await SeedStatisticsRequestAsync(fixture, memberId);
		await MoveStatisticsBorrowerAndActorAsync(fixture, staff, originalScope);
		await WithUnitOfWorkAsync(async () =>
		{
			var scope = await GetRequiredService<IUserScopeAccessService>().GetCurrentUserScopeAsync();
			(await GetRequiredService<LibraryStoredLoanScope>().CanReadAsync(loan.Id, scope)).ShouldBe(originalScope);
			(await GetRequiredService<LibraryStoredReservationScope>().CanReadAsync(requestId, scope)).ShouldBe(originalScope);
			var currentPeople = await GetRequiredService<ILibraryAccessService>().GetMemberIdsAsync(scope);
			if (originalScope) currentPeople.ShouldNotContain(memberId);
			else currentPeople.ShouldContain(memberId);
		});

		var stats = await ReadStatisticsAsync(memberId, "UTC", new(2026, 11, 18, 0, 30, 0, DateTimeKind.Utc));

		stats.CurrentIssue.ShouldBe(originalScope ? 1 : 0);
		stats.ReservedBooks.ShouldBe(originalScope ? 1 : 0);
		stats.OverdueReturnBooks.ShouldBe(0);
	}

	[Theory]
	[InlineData("UTC", 1)]
	[InlineData("Pacific/Honolulu", 0)]
	public async Task H_Statistics_Should_Classify_Confirmed_Civil_Due_Date_With_The_Library_School_Clock(string zone, int overdue)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		await SeedStatisticsLoanAsync(fixture, new(2026, 11, 17));

		var stats = await ReadStatisticsAsync(fixture.Context.StudentId, zone, new(2026, 11, 18, 0, 30, 0, DateTimeKind.Utc));

		stats.CurrentIssue.ShouldBe(1);
		stats.OverdueReturnBooks.ShouldBe(overdue);
	}

	[Fact]
	public async Task H_Statistics_Should_Never_Classify_An_Unconfirmed_Legacy_Raw_Due_Timestamp_As_Overdue()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		await WithUnitOfWorkAsync(async () =>
		{
			var copy = await GetRequiredService<IRepository<BookCopy, Guid>>().GetAsync(fixture.Context.Catalog.CopyId);
			var loan = new BookIssue(Guid.NewGuid(), copy.Id, copy.AccessionNumber, fixture.Context.StudentId, 0,
				new DateTime(2001, 1, 1), new DateTime(2001, 1, 2), copy.TenantId);
			loan.DueDateProvenance.ShouldBe(LibraryCivilDateProvenance.UnconfirmedStoredComponent);
			await GetRequiredService<IRepository<BookIssue, Guid>>().InsertAsync(loan, true);
		});

		var stats = await ReadStatisticsAsync(fixture.Context.StudentId, "UTC", new(2026, 11, 18, 0, 30, 0, DateTimeKind.Utc));

		stats.CurrentIssue.ShouldBe(1);
		stats.OverdueReturnBooks.ShouldBe(0);
	}

	private async Task<LibraryLoanDto> CheckoutStatisticsMemberAsync(StaffScopeFixture fixture, bool staff)
	{
		if (staff) return await CheckoutStaffAsync(fixture);
		var service = GetRequiredService<ILibraryIssuePlanAppService>();
		var plan = await service.PreviewAsync(new() { CopyId = fixture.Context.Catalog.CopyId, MemberId = fixture.Context.StudentId,
			IssueDate = new(2026, 11, 17), RequestedDueDate = new(2026, 11, 24) });
		return (await GetRequiredService<ILibraryLoanAppService>().CheckoutAsync(new() { CopyId = plan.CopyId, MemberId = plan.MemberId,
			AcademicYearId = plan.AcademicYearId, IssueDate = plan.IssueDate, RequestedDueDate = plan.DueDate, Revision = plan.Revision,
			IdempotencyKey = Guid.NewGuid().ToString("N") })).Loan;
	}

	private Task<Guid> SeedStatisticsRequestAsync(StaffScopeFixture fixture, Guid memberId) => WithUnitOfWorkAsync<Guid>(async () =>
	{
		var catalog = fixture.Context.Catalog;
		var book = new Book("Statistics reservation title", "", "Author", "Publisher", "1", "Reading", catalog.CategoryId,
			Language.English, "10+", 100m, 100, 0, catalog.OriginYearId, GetRequiredService<Volo.Abp.MultiTenancy.ICurrentTenant>().Id);
		await GetRequiredService<IRepository<Book, Guid>>().InsertAsync(book, true);
		return (await GetRequiredService<LibraryReservationCoordinator>().CreateAsync(book.Id, memberId, catalog.YearId,
			new(2026, 11, 17), new(2026, 11, 30), LibraryOperationActor.User(GetRequiredService<ICurrentUser>().Id!.Value))).Id;
	});
}
