using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization.Permissions;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryCopyMoveOptions_Tests : ShikshaMitraApplicationTestBase<LibraryCopyLifecyclePermissionTestModule>
{
	[Fact]
	public async Task Move_Options_Should_Return_Real_Persistent_Locations_Without_Unrelated_Settings_Grants()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		(await GetRequiredService<IPermissionChecker>().IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Settings_SectionManagement.View)).ShouldBeFalse();

		var options = await GetRequiredService<ILibraryCopyLifecycleAppService>().GetMoveOptionsAsync(
			context.Catalog.BookId, context.Catalog.CopyId, context.Catalog.YearId);

		options.State.CanMove.ShouldBeTrue();
		options.State.Revision.ShouldNotBeNullOrWhiteSpace();
		var section = options.Sections.Single(row => row.Id == context.Catalog.SectionId);
		section.SectionName.ShouldBe("Main Library");
		section.ShelfDetails.ShouldContain(row => row.Id == context.Catalog.ShelfId && row.ShelfName == "A1");
		section.ShelfDetails.ShouldContain(row => row.Id == context.OtherShelfId && row.ShelfName == "B1");
	}

	[Fact]
	public async Task A_Copy_With_An_Open_Loan_Should_Not_Offer_Move_Destinations()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync(onLoan: true);

		var options = await GetRequiredService<ILibraryCopyLifecycleAppService>().GetMoveOptionsAsync(
			context.Catalog.BookId, context.Catalog.CopyId, context.Catalog.YearId);

		options.State.CanMove.ShouldBeFalse();
		options.Sections.ShouldBeEmpty();
	}

	[Fact]
	public async Task Move_Options_Should_Reject_An_Unknown_Academic_Context()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();

		await Should.ThrowAsync<BusinessException>(() => GetRequiredService<ILibraryCopyLifecycleAppService>()
			.GetMoveOptionsAsync(context.Catalog.BookId, context.Catalog.CopyId, Guid.NewGuid()));
	}

	[Fact]
	public async Task Foreign_Locations_Should_Not_Be_Offered_Or_Accepted_As_New_Destinations()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		LibraryCopyLifecycleTestContext foreign;
		using (LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider)) foreign = await SeedAsync();
		var lifecycle = GetRequiredService<ILibraryCopyLifecycleAppService>();
		var options = await lifecycle.GetMoveOptionsAsync(context.Catalog.BookId, context.Catalog.CopyId, context.Catalog.YearId);

		options.Sections.Select(row => row.Id).ShouldNotContain(foreign.Catalog.SectionId);
		await Should.ThrowAsync<BusinessException>(() => lifecycle.MoveAsync(context.Catalog.BookId, context.Catalog.CopyId, new()
		{
			SectionId = foreign.Catalog.SectionId, ShelfId = foreign.Catalog.ShelfId,
			Reason = "Requested destination", Revision = options.State.Revision
		}));
		(await lifecycle.GetEventsAsync(context.Catalog.BookId, context.Catalog.CopyId)).TotalCount.ShouldBe(0);
	}

	private Task<LibraryCopyLifecycleTestContext> SeedAsync(bool onLoan = false) =>
		WithUnitOfWorkAsync(() => LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider, onLoan));
}
