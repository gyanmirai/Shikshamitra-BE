using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Reservations;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.EducationBoards;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryStaffRecordedScope_Tests
{
	[Fact]
	public async Task Checkout_Should_Capture_All_Actual_Assignment_Tuples_With_Exact_Source_And_Event_References()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		using var activity = new Activity("Immutable staff capture checkout").SetIdFormat(ActivityIdFormat.W3C).Start();
		using var commands = new LibraryLookupSqlCapture(activity.TraceId);
		var loan = await CheckoutStaffAsync(fixture);
		commands.Commands.Where(sql => sql.TrimStart().StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase) &&
			sql.Contains("AppLibraryBorrowerScopeCaptures", StringComparison.Ordinal)).ShouldBeEmpty();

		await WithUnitOfWorkAsync(async () =>
		{
			loan.BorrowerScopeCaptureId.ShouldNotBeNull();
			loan.BorrowerScopeCaptureVersion.ShouldBe(1);
			loan.BorrowerScopeProvenance.ShouldBe(1);
			var capture = await GetRequiredService<IRepository<LibraryBorrowerScopeCapture, Guid>>().GetAsync(loan.BorrowerScopeCaptureId!.Value);
			capture.SourceKind.ShouldBe(LibraryBorrowerScopeSourceKind.Loan);
			capture.SourceId.ShouldBe(loan.Id);
			capture.CanonicalEmployeeId.ShouldBe(fixture.EmployeeId);
			capture.AcademicYearId.ShouldBe(fixture.Context.Catalog.YearId);
			capture.TenantId.ShouldBe(GetRequiredService<ICurrentTenant>().Id);
			capture.IsComplete.ShouldBeTrue();
			capture.ActorId.ShouldBe(GetRequiredService<ICurrentUser>().Id);
			var tuples = await GetRequiredService<IRepository<LibraryBorrowerAssignmentSnapshot, Guid>>().GetListAsync(row => row.CaptureId == capture.Id);
			tuples.Count.ShouldBe(2);
			tuples.Select(row => row.StageId).Order().ShouldBe(new[] { fixture.Context.StageId, fixture.SecondStageId }.Order());
			tuples.ShouldAllBe(row => row.MediumId == fixture.Context.MediumId && row.BoardId == fixture.Context.BoardId && row.AcademicYearId == fixture.Context.Catalog.YearId);
			var sourceIds = await GetRequiredService<IRepository<EmployeeOrganizationAssignment, Guid>>().GetListAsync(row => row.EmployeeId == fixture.EmployeeId);
			tuples.Select(row => row.OrganizationAssignmentId).Order().ShouldBe(sourceIds.Select(row => row.Id).Order());
			var audit = (await GetRequiredService<IRepository<LibraryLoanEvent, Guid>>().GetListAsync(row => row.LoanId == loan.Id)).Single();
			audit.BeforeBorrowerScopeCaptureId.ShouldBeNull();
			audit.AfterBorrowerScopeCaptureId.ShouldBe(capture.Id);
		});
	}

	[Fact]
	public async Task Recorded_Assignment_Scope_Should_Survive_Deletion_Of_The_Mutable_Stage_Row()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		var loan = await CheckoutStaffAsync(fixture);
		await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<EducationMediumStage, Guid>>().DeleteAsync(fixture.Context.StageId, true));
		await MoveStaffAsync(fixture);
		await SetReaderStageAsync(fixture, fixture.Context.StageId);
		var scope = await GetRequiredService<IUserScopeAccessService>().GetCurrentUserScopeAsync();

		(await WithUnitOfWorkAsync(() => GetRequiredService<LibraryStoredLoanScope>().CanReadAsync(loan.Id, scope))).ShouldBeTrue();
		(await GetRequiredService<ILibraryLoanAppService>().GetAsync(loan.Id)).Id.ShouldBe(loan.Id);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Missing_Or_Broken_Staff_Capture_Should_Fail_Closed_For_Narrow_Readers_But_Remain_Reviewable_Tenantwide(bool brokenTuple)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		if (brokenTuple) await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<EducationMediumStage, Guid>>().DeleteAsync(fixture.SecondStageId, true));
		var loanId = await SeedStaffSourceAsync(fixture, capture: brokenTuple);
		var wide = await GetRequiredService<ILibraryLoanAppService>().GetAsync(loanId);
		wide.Id.ShouldBe(loanId);
		if (brokenTuple) await WithUnitOfWorkAsync(async () =>
		{
			var group = await GetRequiredService<IRepository<LibraryBorrowerScopeCapture, Guid>>().GetAsync(wide.BorrowerScopeCaptureId!.Value);
			group.IsComplete.ShouldBeFalse();
			group.IncompleteReason.ShouldNotBeNullOrWhiteSpace();
			var tuples = await GetRequiredService<IRepository<LibraryBorrowerAssignmentSnapshot, Guid>>().GetListAsync(row => row.CaptureId == group.Id);
			tuples.Count.ShouldBe(2);
			tuples.Single(row => row.StageId == fixture.SecondStageId).MediumId.ShouldBeNull();
		});
		await SetReaderStageAsync(fixture, fixture.Context.StageId);

		(await Should.ThrowAsync<BusinessException>(() => GetRequiredService<ILibraryLoanAppService>().GetAsync(loanId))).Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
	}

	[Theory]
	[InlineData("Version")]
	[InlineData("Provenance")]
	[InlineData("Employee")]
	[InlineData("Source")]
	public async Task A_Capture_From_Another_Source_Or_Mismatched_Binding_Should_Not_Authorize_The_Loan(string mismatch)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		var loan = await CheckoutStaffAsync(fixture);
		await WithUnitOfWorkAsync(async () =>
		{
			var source = await GetRequiredService<IRepository<BookIssue, Guid>>().GetAsync(loan.Id);
			if (mismatch == "Version") typeof(BookIssue).GetProperty(nameof(BookIssue.BorrowerScopeCaptureVersion))!.SetValue(source, 99);
			if (mismatch == "Provenance") typeof(BookIssue).GetProperty(nameof(BookIssue.BorrowerScopeProvenance))!.SetValue(source, LibraryBorrowerScopeProvenance.Unknown);
			if (mismatch == "Employee") source.MemberId = Guid.NewGuid();
			if (mismatch == "Source")
			{
				var other = await GetRequiredService<LibraryStaffScopeCaptureService>().CaptureAsync(LibraryBorrowerScopeSourceKind.Loan,
					Guid.NewGuid(), fixture.EmployeeId, fixture.Context.Catalog.YearId, LibraryBorrowerScopeProvenance.CapturedAtOperation,
					LibraryOperationActor.User(GetRequiredService<ICurrentUser>().Id!.Value));
				typeof(BookIssue).GetProperty(nameof(BookIssue.BorrowerScopeCaptureId))!.SetValue(source, other.Id);
			}
			await GetRequiredService<IRepository<BookIssue, Guid>>().UpdateAsync(source, true);
		});
		await SetReaderStageAsync(fixture, fixture.Context.StageId);

		(await Should.ThrowAsync<BusinessException>(() => GetRequiredService<ILibraryLoanAppService>().GetAsync(loan.Id))).Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
	}

	[Fact]
	public async Task A_Scope_In_Another_Academic_Year_Should_Not_Grant_Access_Through_A_Captured_Stage_Identifier()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		var loan = await CheckoutStaffAsync(fixture);
		await WithUnitOfWorkAsync(async () =>
		{
			var actor = GetRequiredService<ICurrentUser>().Id!.Value;
			var assignments = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
			await assignments.DeleteAsync(row => row.UserId == actor, true);
			await assignments.InsertAsync(new(Guid.NewGuid(), GetRequiredService<ICurrentTenant>().Id, userId: actor,
				academicYearId: fixture.Context.Catalog.OriginYearId, educationBoardId: fixture.Context.BoardId,
				educationMediumId: fixture.Context.MediumId, educationMediumStageId: fixture.Context.StageId), true);
			await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(actor);
		});

		(await Should.ThrowAsync<BusinessException>(() => GetRequiredService<ILibraryLoanAppService>().GetAsync(loan.Id))).Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
	}

	[Fact]
	public async Task Reservation_Review_Should_Create_A_New_Capture_And_Retain_The_Previous_Group_Children_And_Event_References()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		var app = GetRequiredService<ILibraryReservationAppService>();
		var first = await app.CreateAsync(new() { BookId = fixture.Context.Catalog.BookId, MemberId = fixture.EmployeeId,
			AcademicYearId = fixture.Context.Catalog.YearId, RequestedDate = new(2026, 11, 17), RequestExpiryDate = new(2026, 12, 1) });
		await WithUnitOfWorkAsync(async () =>
		{
			var request = await GetRequiredService<IRepository<BookReservation, Guid>>().GetAsync(first.Id);
			request.RequireReview("Review recorded staff assignments explicitly.");
			await GetRequiredService<IRepository<BookReservation, Guid>>().UpdateAsync(request, true);
		});
		await MoveStaffAsync(fixture);
		var pending = await app.GetAsync(first.Id, fixture.Context.Catalog.YearId);
		var reviewed = await app.ReviewAsync(first.Id, new() { AcademicYearId = fixture.Context.Catalog.YearId, MemberId = fixture.EmployeeId,
			RequestedDate = new(2026, 11, 17), RequestExpiryDate = new(2026, 12, 1), Reason = "Reviewed the actual replacement assignments.", Revision = pending.Revision });

		reviewed.BorrowerScopeCaptureId.ShouldNotBe(first.BorrowerScopeCaptureId);
		reviewed.BorrowerScopeCaptureVersion.ShouldBe(2);
		reviewed.BorrowerScopeProvenance.ShouldBe(2);
		await WithUnitOfWorkAsync(async () =>
		{
			var groups = await GetRequiredService<IRepository<LibraryBorrowerScopeCapture, Guid>>().GetListAsync(row => row.SourceId == first.Id);
			groups.Count.ShouldBe(2);
			var tuples = GetRequiredService<IRepository<LibraryBorrowerAssignmentSnapshot, Guid>>();
			(await tuples.CountAsync(row => row.CaptureId == first.BorrowerScopeCaptureId)).ShouldBe(2);
			(await tuples.GetListAsync(row => row.CaptureId == reviewed.BorrowerScopeCaptureId)).Single().StageId.ShouldBe(fixture.LaterStageId);
			var history = (await GetRequiredService<IRepository<LibraryReservationEvent, Guid>>().GetListAsync(row => row.ReservationId == first.Id && row.EventType == "Reviewed")).Single();
			history.BeforeBorrowerScopeCaptureId.ShouldBe(first.BorrowerScopeCaptureId);
			history.AfterBorrowerScopeCaptureId.ShouldBe(reviewed.BorrowerScopeCaptureId);
		});
		await SetReaderStageAsync(fixture, fixture.Context.StageId);
		await Should.ThrowAsync<BusinessException>(() => app.GetAsync(first.Id, fixture.Context.Catalog.YearId));
		await SetReaderStageAsync(fixture, fixture.LaterStageId);
		(await app.GetAsync(first.Id, fixture.Context.Catalog.YearId)).Id.ShouldBe(first.Id);
	}

	[Fact]
	public async Task Reviewing_Loan_Dates_Should_Not_Recapture_Staff_Assignments_Or_Replace_Their_History()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		var loan = await CheckoutStaffAsync(fixture);
		await MoveStaffAsync(fixture);
		await WithUnitOfWorkAsync(async () =>
		{
			var source = await GetRequiredService<IRepository<BookIssue, Guid>>().GetAsync(loan.Id);
			typeof(BookIssue).GetProperty(nameof(BookIssue.DueDateProvenance))!.SetValue(source, LibraryCivilDateProvenance.UnconfirmedStoredComponent);
			await GetRequiredService<IRepository<BookIssue, Guid>>().UpdateAsync(source, true);
		});
		var app = GetRequiredService<ILibraryLoanAppService>();
		var before = await app.GetAsync(loan.Id);
		var result = await app.ReviewDatesAsync(loan.Id, new() { SchoolDueDate = new(2026, 11, 24), Reason = "Confirmed the original school due date.",
			Revision = before.Revision, IdempotencyKey = Guid.NewGuid().ToString("N") });

		result.Loan.BorrowerScopeCaptureId.ShouldBe(loan.BorrowerScopeCaptureId);
		result.Loan.BorrowerScopeCaptureVersion.ShouldBe(1);
		await WithUnitOfWorkAsync(async () =>
		{
			(await GetRequiredService<IRepository<LibraryBorrowerScopeCapture, Guid>>().CountAsync(row => row.SourceId == loan.Id)).ShouldBe(1);
			var review = (await GetRequiredService<IRepository<LibraryLoanEvent, Guid>>().GetListAsync(row => row.LoanId == loan.Id && row.EventKind == LibraryLoanEventKind.DatesReviewed)).Single();
			review.BeforeBorrowerScopeCaptureId.ShouldBe(loan.BorrowerScopeCaptureId);
			review.AfterBorrowerScopeCaptureId.ShouldBe(loan.BorrowerScopeCaptureId);
		});
	}

	[Fact]
	public async Task Staff_Loan_Count_And_Page_Should_Use_Recorded_Exists_Sql_Without_Current_Organization_Assignment_Joins()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		await CheckoutStaffAsync(fixture);
		await SeedStaffSourceAsync(fixture, capture: true);
		await MoveStaffAsync(fixture);
		await SetReaderStageAsync(fixture, fixture.Context.StageId);
		using var activity = new Activity("Recorded staff loan paging").SetIdFormat(ActivityIdFormat.W3C).Start();
		using var commands = new LibraryLookupSqlCapture(activity.TraceId);

		var first = await GetRequiredService<ILibraryCirculationReadAppService>().GetListAsync(pageSize: 1);
		var second = await GetRequiredService<ILibraryCirculationReadAppService>().GetListAsync(page: 2, pageSize: 1);

		first.TotalCount.ShouldBe(2);
		first.TotalPages.ShouldBe(2);
		first.Items.Single().TransactionId.ShouldNotBe(second.Items.Single().TransactionId);
		var counts = commands.Commands.Where(sql => sql.Contains("COUNT", StringComparison.OrdinalIgnoreCase) && sql.Contains("AppBookIssues", StringComparison.Ordinal)).ToList();
		counts.ShouldNotBeEmpty();
		foreach (var sql in counts)
		{
			sql.ShouldContain("AppLibraryBorrowerScopeCaptures");
			sql.ShouldContain("AppLibraryBorrowerAssignmentSnapshots");
			sql.ShouldNotContain("AppEmployeeOrganizationAssignments");
		}
	}

	private Task<Guid> SeedStaffSourceAsync(StaffScopeFixture fixture, bool capture) => WithUnitOfWorkAsync(async () =>
	{
		var copy = new BookCopy(Guid.NewGuid(), fixture.Context.Catalog.BookId, 20, $"STAFF-{Guid.NewGuid():N}", fixture.Context.Catalog.SectionId,
			fixture.Context.Catalog.ShelfId, GetRequiredService<ICurrentTenant>().Id);
		await GetRequiredService<IRepository<BookCopy, Guid>>().InsertAsync(copy, true);
		var loan = BookIssue.CreateCivil(Guid.NewGuid(), copy.TenantId, copy.Id, copy.AccessionNumber, fixture.EmployeeId, 1,
			new(2026, 11, 17), new(2026, 11, 24), new(fixture.Context.Catalog.YearId, null, null, null, null, null,
				"{}", "{}", "{}", GetRequiredService<ICurrentUser>().Id!.Value, new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.Zero)));
		if (capture) loan.BindStaffScopeCapture(await GetRequiredService<LibraryStaffScopeCaptureService>().CaptureAsync(LibraryBorrowerScopeSourceKind.Loan,
			loan.Id, fixture.EmployeeId, fixture.Context.Catalog.YearId, LibraryBorrowerScopeProvenance.CapturedAtOperation,
			LibraryOperationActor.User(GetRequiredService<ICurrentUser>().Id!.Value)));
		await GetRequiredService<IRepository<BookIssue, Guid>>().InsertAsync(loan, true);
		return loan.Id;
	});
}
