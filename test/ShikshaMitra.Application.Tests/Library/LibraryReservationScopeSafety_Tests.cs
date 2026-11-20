using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Policies;
using ShikshaMitra.Library.Reservations;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryReservationScopeSafety_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Fact]
	public async Task Tenant_Wide_Operator_Should_Find_And_Cancel_An_Orphan_Legacy_Head_Without_Inventing_A_Borrower()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await ContextAsync();
		var orphan = await OrphanAsync(context);

		var list = await App.GetListAsync(context.Catalog.BookId, context.Catalog.YearId);

		list.TotalCount.ShouldBe(1);
		var row = list.Items.Single();
		row.Id.ShouldBe(orphan.Id);
		row.MemberId.ShouldBe(orphan.MemberId);
		row.RequiresReview.ShouldBeTrue();
		row.CanReview.ShouldBeTrue();
		row.CanCancel.ShouldBeTrue();
		(await App.GetAsync(orphan.Id, context.Catalog.YearId)).MemberId.ShouldBe(orphan.MemberId);
		await App.CancelAsync(orphan.Id, new() { AcademicYearId = context.Catalog.YearId,
			Reason = "Legacy borrower cannot be established; cancelled after reviewing the original request", Revision = row.Revision });
		await WithUnitOfWorkAsync(async () =>
		{
			var retained = await GetRequiredService<IRepository<BookReservation, Guid>>().GetAsync(orphan.Id);
			retained.State.ShouldBe(LibraryReservationState.Cancelled);
			retained.MemberId.ShouldBe(orphan.MemberId);
			retained.CanonicalMemberId.ShouldBeNull();
		});
	}

	[Fact]
	public async Task Tenant_Wide_Operator_Should_Explicitly_Rebind_An_Orphan_Without_Overwriting_Original_Identity_Or_Dates()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await ContextAsync();
		var orphan = await OrphanAsync(context);

		var reviewed = await App.ReviewAsync(orphan.Id, new() { AcademicYearId = context.Catalog.YearId,
			MemberId = context.StudentId, RequestedDate = new(2026, 11, 17), RequestExpiryDate = new(2026, 12, 1),
			Reason = "Verified the original paper request with the borrower and admitted it at the current queue tail", Revision = orphan.Revision });

		reviewed.MemberId.ShouldBe(context.StudentId);
		reviewed.RequiresReview.ShouldBeFalse();
		await WithUnitOfWorkAsync(async () =>
		{
			var retained = await GetRequiredService<IRepository<BookReservation, Guid>>().GetAsync(orphan.Id);
			retained.MemberId.ShouldBe(orphan.MemberId);
			retained.OriginalMemberId.ShouldBe(orphan.MemberId);
			retained.CanonicalMemberId.ShouldBe(context.StudentId);
			retained.RequestedDate.ShouldBe(orphan.RequestedDate);
			retained.ReserveDueDate.ShouldBe(orphan.ReserveDueDate);
		});
	}

	[Fact]
	public async Task A_Scoped_Operator_Should_Remain_Unable_To_Read_Or_Resolve_An_Unknown_Year_Orphan()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await ContextAsync();
		var orphan = await OrphanAsync(context);
		await RestrictAsync(context);

		(await App.GetListAsync(context.Catalog.BookId, context.Catalog.YearId)).TotalCount.ShouldBe(0);
		await Should.ThrowAsync<BusinessException>(() => App.GetAsync(orphan.Id, context.Catalog.YearId));
		await Should.ThrowAsync<BusinessException>(() => App.CancelAsync(orphan.Id, new()
		{
			AcademicYearId = context.Catalog.YearId, Reason = "Scoped cancellation attempt", Revision = orphan.Revision
		}));
		await WithUnitOfWorkAsync(async () =>
			(await GetRequiredService<IRepository<BookReservation, Guid>>().GetAsync(orphan.Id)).State.ShouldBe(LibraryReservationState.NeedsReview));
	}

	[Fact]
	public async Task Current_Year_Scope_Should_Not_Count_An_Old_Year_Request_For_A_Now_Current_Student()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await ContextAsync();
		await OldYearAsync(context);
		await RestrictAsync(context);

		var list = await App.GetListAsync(context.Catalog.BookId, context.Catalog.YearId);

		list.TotalCount.ShouldBe(0);
		list.Items.ShouldBeEmpty();
	}

	[Fact]
	public async Task Tenant_Wide_Lists_Should_Also_Respect_The_Selected_Stored_Request_Year()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await ContextAsync();
		var old = await OldYearAsync(context);

		(await App.GetListAsync(context.Catalog.BookId, context.Catalog.YearId)).TotalCount.ShouldBe(0);
		(await App.GetListAsync(context.Catalog.BookId, context.Catalog.OriginYearId)).Items.Single().Id.ShouldBe(old.Id);
	}

	[Fact]
	public async Task A_Current_Year_Query_Parameter_Should_Not_Authorize_A_Direct_Old_Year_Request_Read()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await ContextAsync();
		var old = await OldYearAsync(context);
		await RestrictAsync(context);

		await Should.ThrowAsync<BusinessException>(() => App.GetAsync(old.Id, context.Catalog.YearId));
	}

	[Fact]
	public async Task A_Current_Year_Query_Parameter_Should_Not_Authorize_Cancelling_An_Old_Year_Request()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await ContextAsync();
		var old = await OldYearAsync(context);
		await RestrictAsync(context);

		await Should.ThrowAsync<BusinessException>(() => App.CancelAsync(old.Id, new()
		{
			AcademicYearId = context.Catalog.YearId, Reason = "Wrong persisted-year attempt", Revision = old.Revision
		}));
		await WithUnitOfWorkAsync(async () =>
			(await GetRequiredService<IRepository<BookReservation, Guid>>().GetAsync(old.Id)).State.ShouldBe(LibraryReservationState.Waiting));
	}

	private ILibraryReservationAppService App => GetRequiredService<ILibraryReservationAppService>();
	private Task<LibraryCopyLifecycleTestContext> ContextAsync() =>
		WithUnitOfWorkAsync(() => LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider));
	private Task<BookReservation> OrphanAsync(LibraryCopyLifecycleTestContext context) => WithUnitOfWorkAsync(() =>
		GetRequiredService<IRepository<BookReservation, Guid>>().InsertAsync(new(Guid.NewGuid(), context.Catalog.BookId,
			Guid.NewGuid(), new DateTime(2025, 2, 3, 17, 45, 0, DateTimeKind.Utc), new DateTime(2025, 2, 10, 18, 15, 0, DateTimeKind.Utc),
			GetRequiredService<ICurrentTenant>().Id), true));
	private Task<BookReservation> OldYearAsync(LibraryCopyLifecycleTestContext context) => WithUnitOfWorkAsync(() =>
		GetRequiredService<IRepository<BookReservation, Guid>>().InsertAsync(BookReservation.Create(Guid.NewGuid(),
			GetRequiredService<ICurrentTenant>().Id, context.Catalog.BookId, context.StudentId, context.StudentId, 0,
			context.Catalog.OriginYearId, 1, new(2025, 11, 17), new(2025, 12, 1), JsonSerializer.Serialize(new LibraryBorrowingPolicySnapshotDto
			{
				IsLegacyManualDate = true, Role = 0, Terms = new() { MaxBooksAtTime = 4, ReservationLimit = 1 }
			})), true));
	private Task RestrictAsync(LibraryCopyLifecycleTestContext context) => WithUnitOfWorkAsync(async () =>
	{
		var userId = GetRequiredService<ICurrentUser>().Id!.Value;
		var scopes = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
		await scopes.DeleteAsync(row => row.UserId == userId, true);
		await scopes.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), GetRequiredService<ICurrentTenant>().Id, userId: userId,
			academicYearId: context.Catalog.YearId, educationBoardId: context.BoardId, educationMediumId: context.MediumId,
			educationMediumStageId: context.StageId, standardId: context.BorrowerClassId), true);
		await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(userId);
	});
}
