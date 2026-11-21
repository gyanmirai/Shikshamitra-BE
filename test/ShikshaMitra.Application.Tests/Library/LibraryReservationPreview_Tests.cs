using System;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Reservations;
using Shouldly;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryReservationPreview_Tests : ShikshaMitraApplicationTestBase<LibraryReservationMemberLookupTestModule>
{
	[Fact]
	public async Task Review_Preview_Should_Exclude_The_Exact_Existing_Request_From_Duplicate_And_Quota_Checks_Without_Mutating_It()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await WithUnitOfWorkAsync(() => LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider));
		var request = BookReservation.Create(Guid.NewGuid(), GetRequiredService<Volo.Abp.MultiTenancy.ICurrentTenant>().Id,
			context.Catalog.BookId, context.StudentId, context.StudentId, 0, context.Catalog.YearId, 1,
			new(2026, 11, 17), new(2026, 12, 1), "{}");
		request.RequireReview("Review the saved admission evidence.");
		await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<BookReservation, Guid>>().InsertAsync(request, true));

		var result = await GetRequiredService<ILibraryReservationAppService>().PreviewReviewAsync(request.Id, new()
		{
			AcademicYearId = context.Catalog.YearId, MemberId = context.StudentId, RequestedDate = new(2026, 11, 17),
			RequestExpiryDate = new(2026, 12, 1), Revision = request.Revision
		});

		result.ReservationId.ShouldBe(request.Id);
		result.CanonicalMemberId.ShouldBe(context.StudentId);
		result.RequiresExplicitExpiry.ShouldBeTrue();
		result.CanReview.ShouldBeTrue();
		result.BlockedReasons.ShouldBeEmpty();
		await WithUnitOfWorkAsync(async () =>
		{
			(await GetRequiredService<IRepository<BookReservation, Guid>>().GetAsync(request.Id)).State.ShouldBe(LibraryReservationState.NeedsReview);
			(await GetRequiredService<IRepository<LibraryHold, Guid>>().CountAsync()).ShouldBe(0);
			(await GetRequiredService<IRepository<LibraryReservationEvent, Guid>>().CountAsync()).ShouldBe(0);
		});
	}

	[Fact]
	public async Task Reserve_Only_Operator_Should_Learn_Manual_Expiry_Requirement_Without_Administration_Or_Circulation_Grants_And_Without_Writes()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await WithUnitOfWorkAsync(() => LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider));
		var permissions = GetRequiredService<IPermissionChecker>();
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Settings_BorrowingRules.View).Returns(false);
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Settings_BorrowingRules.Add).Returns(false);
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.IssueReturn.View).Returns(false);
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.IssueReturn.Issue).Returns(false);
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.IssueReturn.Return).Returns(false);
		(await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.IssueReturn.Reserve)).ShouldBeTrue();

		var result = await GetRequiredService<ILibraryReservationAppService>().PreviewAsync(new()
		{
			BookId = context.Catalog.BookId, MemberId = context.StudentId, AcademicYearId = context.Catalog.YearId,
			RequestedDate = new(2026, 11, 17), RequestExpiryDate = null
		});

		result.CanonicalMemberId.ShouldBe(context.StudentId);
		result.AcademicYearId.ShouldBe(context.Catalog.YearId);
		result.Policy!.IsLegacyManualDate.ShouldBeTrue();
		result.RequiresExplicitExpiry.ShouldBeTrue();
		result.CanCreate.ShouldBeFalse();
		result.BlockedReasons.ShouldContain(reason => reason.Contains("expiry", StringComparison.OrdinalIgnoreCase));
		await WithUnitOfWorkAsync(async () =>
		{
			(await GetRequiredService<IRepository<BookReservation, Guid>>().CountAsync()).ShouldBe(0);
			(await GetRequiredService<IRepository<LibraryHold, Guid>>().CountAsync()).ShouldBe(0);
			(await GetRequiredService<IRepository<LibraryCopyEvent, Guid>>().CountAsync()).ShouldBe(0);
			(await GetRequiredService<IRepository<BookCopy, Guid>>().GetAsync(context.Catalog.CopyId)).Status.ShouldBe(BookCopyStatus.Available);
		});
	}
}
