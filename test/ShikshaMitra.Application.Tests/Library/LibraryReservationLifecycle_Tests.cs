using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Members;
using ShikshaMitra.Library.Policies;
using ShikshaMitra.Library.Reservations;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryReservationLifecycle_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private static readonly DateOnly ManualDate = new(2026, 11, 17);

	[Fact]
	public async Task Canonical_Manual_Request_Should_Not_Persist_Fake_Legacy_Timestamps_And_Should_Record_System_Allocation()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		var request = await CreateAsync(context, context.School.StudentId);

		request.LegacyRequestedDate.ShouldBeNull();
		request.LegacyReserveDueDate.ShouldBeNull();
		request.RequestedDate.ShouldBe(ManualDate);
		request.State.ShouldBe((int)LibraryReservationState.Ready);
		request.Hold!.PickupDeadline.ShouldBe(ManualDate.AddDays(14));
		await WithUnitOfWorkAsync(async () =>
		{
			var copyEvent = (await GetRequiredService<IRepository<LibraryCopyEvent, Guid>>().GetListAsync(row => row.CopyId == context.School.Catalog.CopyId)).Single();
			copyEvent.EventType.ShouldBe(LibraryCopyEventType.HoldAllocated);
			copyEvent.ActorKind.ShouldBe(LibraryActorKind.System);
			copyEvent.ActorId.ShouldBeNull();
			copyEvent.SystemPurpose.ShouldBe(LibrarySystemPurpose.HoldAllocator);
		});
	}

	[Fact]
	public async Task Backdated_Requested_Date_Should_Not_Jump_The_Server_Queue()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		var head = await CreateAsync(context, context.School.StudentId);
		var later = await CreateAsync(context, context.OtherStudentId, ManualDate.AddDays(-7));

		head.State.ShouldBe((int)LibraryReservationState.Ready);
		later.State.ShouldBe((int)LibraryReservationState.Waiting);
		await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<BookReservation, Guid>>();
			(await repository.GetAsync(head.Id)).QueueSequence!.Value.ShouldBeLessThan((await repository.GetAsync(later.Id)).QueueSequence!.Value);
		});
	}

	[Fact]
	public async Task Cancellation_Should_Release_And_Promote_Atomically_Without_Deleting_Requests_Or_History()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		var head = await CreateAsync(context, context.School.StudentId);
		var next = await CreateAsync(context, context.OtherStudentId);

		await App.CancelAsync(head.Id, new() { AcademicYearId = context.School.Catalog.YearId, Reason = "Borrower no longer needs the title", Revision = head.Revision });

		(await App.GetAsync(next.Id, context.School.Catalog.YearId)).State.ShouldBe((int)LibraryReservationState.Ready);
		(await App.GetAsync(head.Id, context.School.Catalog.YearId)).State.ShouldBe((int)LibraryReservationState.Cancelled);
		await WithUnitOfWorkAsync(async () =>
		{
			var holds = await GetRequiredService<IRepository<LibraryHold, Guid>>().GetListAsync(row => row.BookId == context.School.Catalog.BookId);
			holds.Count.ShouldBe(2);
			holds.Count(row => row.State == LibraryHoldState.Allocated).ShouldBe(1);
			holds.Single(row => row.ReservationId == head.Id).State.ShouldBe(LibraryHoldState.Released);
			(await GetRequiredService<IRepository<LibraryReservationEvent, Guid>>().CountAsync(row => row.ReservationId == head.Id)).ShouldBe(3);
		});
	}

	[Fact]
	public async Task A_Stale_Waiting_Form_Should_Not_Cancel_A_Newly_Ready_Hold()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		var head = await CreateAsync(context, context.School.StudentId);
		var waiting = await CreateAsync(context, context.OtherStudentId);
		await App.CancelAsync(head.Id, new() { AcademicYearId = context.School.Catalog.YearId, Reason = "Cancelled by borrower", Revision = head.Revision });

		var error = await Should.ThrowAsync<BusinessException>(() => App.CancelAsync(waiting.Id, new()
		{
			AcademicYearId = context.School.Catalog.YearId, Reason = "Old form cancellation", Revision = waiting.Revision
		}));

		error.Code.ShouldBe(LibraryReservationErrorCodes.Conflict);
		(await App.GetAsync(waiting.Id, context.School.Catalog.YearId)).State.ShouldBe((int)LibraryReservationState.Ready);
	}

	[Fact]
	public async Task A_Checkout_Business_Date_Should_Not_Advance_The_Hold_Expiry_Clock()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		await GetRequiredService<ILibraryCalendarAppService>().UpdateSettingsAsync(new() { TimeZoneId = "America/New_York" });
		var today = await WithUnitOfWorkAsync(() => GetRequiredService<LibraryCalendarService>().TodayAsync());
		var head = await CreateAsync(context, context.School.StudentId, today);
		await CreateAsync(context, context.OtherStudentId, today);
		var copy = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<BookCopy, Guid>>().GetAsync(context.School.Catalog.CopyId));

		await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() => GetRequiredService<LibraryReservationCoordinator>()
			.PrepareIssueAsync(copy, context.OtherStudentId, today.AddDays(100))));

		(await App.GetAsync(head.Id, context.School.Catalog.YearId)).State.ShouldBe((int)LibraryReservationState.Ready);
	}

	[Fact]
	public async Task Unreviewed_Legacy_Order_Should_Block_New_Allocation_Without_Automatically_Expiring_Raw_Dates()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<BookReservation, Guid>>().InsertAsync(
			new BookReservation(Guid.NewGuid(), context.School.Catalog.BookId, context.School.StudentId,
				new DateTime(2025, 1, 1), new DateTime(2025, 1, 2), GetRequiredService<ICurrentTenant>().Id), true));

		var waiting = await CreateAsync(context, context.OtherStudentId);

		waiting.State.ShouldBe((int)LibraryReservationState.Waiting);
		waiting.Hold.ShouldBeNull();
		await WithUnitOfWorkAsync(async () =>
			(await GetRequiredService<IRepository<BookCopy, Guid>>().GetAsync(context.School.Catalog.CopyId)).Status.ShouldBe(BookCopyStatus.Available));
	}

	[Fact]
	public async Task Review_Command_Should_Respect_The_Reservation_Limit_Across_Other_Active_Titles()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		var existing = await CreateAsync(context, context.School.StudentId);
		var target = await WithUnitOfWorkAsync(async () =>
		{
			var book = new Book("Another requested title", string.Empty, "School Author", "School Press", "1", "Reading",
				context.School.Catalog.CategoryId, Language.English, "10+", 50m, 80, 0, context.School.Catalog.YearId,
				GetRequiredService<ICurrentTenant>().Id);
			await GetRequiredService<IRepository<Book, Guid>>().InsertAsync(book, true);
			return await GetRequiredService<IRepository<BookReservation, Guid>>().InsertAsync(new(Guid.NewGuid(), book.Id,
				context.School.StudentId, new DateTime(2025, 1, 1), new DateTime(2025, 1, 8), GetRequiredService<ICurrentTenant>().Id), true);
		});

		var error = await Should.ThrowAsync<BusinessException>(() => App.ReviewAsync(target.Id, new()
		{
			AcademicYearId = context.School.Catalog.YearId, MemberId = context.School.StudentId,
			RequestedDate = ManualDate, RequestExpiryDate = ManualDate.AddDays(14), Revision = target.Revision,
			Reason = "Reviewed a second title while another request already fills the reservation allowance"
		}));

		error.Code.ShouldBe(LibraryErrorCodes.ReservationLimitExceeded);
		await WithUnitOfWorkAsync(async () =>
		{
			(await GetRequiredService<IRepository<BookReservation, Guid>>().GetAsync(target.Id)).State.ShouldBe(LibraryReservationState.NeedsReview);
			(await GetRequiredService<IRepository<LibraryHold, Guid>>().AnyAsync(row => row.ReservationId == target.Id)).ShouldBeFalse();
		});
		(await App.GetAsync(existing.Id, context.School.Catalog.YearId)).State.ShouldBe((int)LibraryReservationState.Ready);
	}

	[Fact]
	public async Task Reviewing_Legacy_Context_Should_Preserve_The_Original_Raw_Timestamps()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		var rawRequested = new DateTime(2025, 1, 1, 19, 45, 12, DateTimeKind.Utc);
		var rawExpiry = rawRequested.AddDays(7);
		var legacy = new BookReservation(Guid.NewGuid(), context.School.Catalog.BookId, context.School.StudentId,
			rawRequested, rawExpiry, GetRequiredService<ICurrentTenant>().Id);
		await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<BookReservation, Guid>>().InsertAsync(legacy, true));

		var reviewed = await App.ReviewAsync(legacy.Id, new() { AcademicYearId = context.School.Catalog.YearId,
			MemberId = context.School.StudentId, RequestedDate = ManualDate, RequestExpiryDate = ManualDate.AddDays(14),
			Reason = "Verified borrower and manually admitted this legacy request at the end of the queue", Revision = legacy.Revision });

		reviewed.LegacyRequestedDate.ShouldBe(rawRequested);
		reviewed.LegacyReserveDueDate.ShouldBe(rawExpiry);
		reviewed.RequestedDateProvenance.ShouldBe((int)LibraryCivilDateProvenance.ReviewedLegacy);
		reviewed.State.ShouldBe((int)LibraryReservationState.Ready);
	}

	[Fact]
	public async Task Scoped_Responses_Should_Hide_The_Head_While_Global_Allocation_Still_Protects_Its_Hold()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		await CreateAsync(context, context.School.StudentId);
		var own = await CreateAsync(context, context.OtherStudentId);
		await RestrictToOtherClassAsync(context);

		var rows = await App.GetListAsync(context.School.Catalog.BookId, context.School.Catalog.YearId);

		rows.TotalCount.ShouldBe(1);
		rows.Items.Single().Id.ShouldBe(own.Id);
		var copy = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<BookCopy, Guid>>().GetAsync(context.School.Catalog.CopyId));
		var priority = await WithUnitOfWorkAsync(() => GetRequiredService<LibraryReservationCoordinator>().ReadIssuePriorityAsync(copy, context.OtherStudentId));
		priority.CanIssue.ShouldBeFalse();
		priority.BlockedReason!.ShouldNotContain(context.School.StudentId.ToString());
	}

	[Fact]
	public async Task Priority_Read_Should_Not_Allocate_Or_Append_Events()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		var request = await CreateAsync(context, context.School.StudentId);
		var copy = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<BookCopy, Guid>>().GetAsync(context.School.Catalog.CopyId));
		var count = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<LibraryReservationEvent, Guid>>().CountAsync());

		var priority = await WithUnitOfWorkAsync(() => GetRequiredService<LibraryReservationCoordinator>().ReadIssuePriorityAsync(copy, context.School.StudentId));

		priority.CanIssue.ShouldBeTrue();
		priority.Revision.ShouldNotBeNullOrWhiteSpace();
		(await App.GetAsync(request.Id, context.School.Catalog.YearId)).Revision.ShouldBe(request.Revision);
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<LibraryReservationEvent, Guid>>().CountAsync())).ShouldBe(count);
	}

	[Fact]
	public async Task Equal_Time_Conflicting_Membership_States_Should_Require_Review_Instead_Of_Guid_Chronology()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		await WithUnitOfWorkAsync(async () =>
		{
			var time = new DateTime(2026, 1, 1);
			var repository = GetRequiredService<IRepository<LibraryMember, Guid>>();
			await repository.InsertAsync(new LibraryMember(Guid.NewGuid(), GetRequiredService<ICurrentTenant>().Id, context.School.StudentId, "OLD-A", true) { CreationTime = time }, true);
			await repository.InsertAsync(new LibraryMember(Guid.NewGuid(), GetRequiredService<ICurrentTenant>().Id, context.School.StudentId, "OLD-B", false) { CreationTime = time }, true);
		});

		var facts = (await WithUnitOfWorkAsync(() => GetRequiredService<LibraryReservationEligibilityReader>().ReadAsync([context.School.StudentId])))[context.School.StudentId];

		facts.Eligibility.ShouldBe(LibraryReservationEligibility.NeedsReview);
	}

	[Fact]
	public async Task Foreign_Tenant_Requests_Should_Not_Be_Read_Or_Cancelled()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		LibraryReservationDto foreign;
		using (LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider))
		{
			var other = await SeedAsync();
			foreign = await CreateAsync(other, other.School.StudentId);
		}

		await Should.ThrowAsync<EntityNotFoundException>(() => App.GetAsync(foreign.Id, context.School.Catalog.YearId));
		await Should.ThrowAsync<EntityNotFoundException>(() => App.CancelAsync(foreign.Id, new()
		{
			AcademicYearId = context.School.Catalog.YearId, Reason = "Unauthorized attempt", Revision = foreign.Revision
		}));
	}

	private ILibraryReservationAppService App => GetRequiredService<ILibraryReservationAppService>();
	private Task<LibraryReservationDto> CreateAsync(Context context, Guid memberId, DateOnly? date = null) => App.CreateAsync(new()
	{
		BookId = context.School.Catalog.BookId, AcademicYearId = context.School.Catalog.YearId, MemberId = memberId,
		RequestedDate = date ?? ManualDate, RequestExpiryDate = (date ?? ManualDate).AddDays(14)
	});
	private Task<Context> SeedAsync() => WithUnitOfWorkAsync(async () =>
	{
		var school = await LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider);
		var student = Student.CreateDirectRegistration(Guid.NewGuid(), GetRequiredService<ICurrentTenant>().Id, "Later", null, "Borrower",
			school.Catalog.YearId, school.BoardId, school.MediumId, school.StageId, school.OtherClassId, null,
			new DateTimeOffset(2014, 1, 2, 0, 0, 0, TimeSpan.Zero), GenderEnum.Male, "8888888888");
		student.AdditionalDetail!.IsLibraryMember = true;
		await GetRequiredService<IRepository<Student, Guid>>().InsertAsync(student, true);
		return new Context(school, student.Id);
	});
	private Task RestrictToOtherClassAsync(Context context) => WithUnitOfWorkAsync(async () =>
	{
		var userId = GetRequiredService<ICurrentUser>().Id!.Value;
		var repository = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
		await repository.DeleteAsync(row => row.UserId == userId, true);
		await repository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), GetRequiredService<ICurrentTenant>().Id, userId: userId,
			academicYearId: context.School.Catalog.YearId, educationBoardId: context.School.BoardId, educationMediumId: context.School.MediumId,
			educationMediumStageId: context.School.StageId, standardId: context.School.OtherClassId), true);
		await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(userId);
	});
	private sealed record Context(LibraryCopyLifecycleTestContext School, Guid OtherStudentId);
}
