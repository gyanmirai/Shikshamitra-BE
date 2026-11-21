using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryCopyLocationSnapshot_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Fact]
	public async Task A_Corrective_Move_Should_Preserve_Orphaned_Previous_Ids_Without_Inventing_A_Location()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var (context, oldSection, oldShelf) = await SeedOrphanedLocationAsync();
		var lifecycle = GetRequiredService<ILibraryCopyLifecycleAppService>();
		var state = await lifecycle.GetStateAsync(context.Catalog.BookId, context.Catalog.CopyId);

		var moved = await lifecycle.MoveAsync(state.BookId, state.CopyId, new()
		{
			SectionId = context.Catalog.SectionId, ShelfId = context.OtherShelfId,
			Reason = "Physical stock check confirmed the correct destination", Revision = state.Revision
		});

		moved.SectionId.ShouldBe(context.Catalog.SectionId);
		moved.ShelfId.ShouldBe(context.OtherShelfId);
		var events = await lifecycle.GetEventsAsync(state.BookId, state.CopyId);
		events.TotalCount.ShouldBe(1);
		events.Items[0].FromSectionId.ShouldBe(oldSection);
		events.Items[0].FromShelfId.ShouldBe(oldShelf);
		events.Items[0].ToSectionId.ShouldBe(context.Catalog.SectionId);
		events.Items[0].ToShelfId.ShouldBe(context.OtherShelfId);
	}

	[Fact]
	public async Task A_Condition_Event_Should_Preserve_Unresolved_Previous_And_Current_Location_Ids()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var (context, oldSection, oldShelf) = await SeedOrphanedLocationAsync();
		var lifecycle = GetRequiredService<ILibraryCopyLifecycleAppService>();
		var state = await lifecycle.GetStateAsync(context.Catalog.BookId, context.Catalog.CopyId);

		var changed = await lifecycle.ChangeConditionAsync(state.BookId, state.CopyId, new()
		{
			Status = BookCopyStatus.Damaged, Reason = "Damage confirmed; location reconciliation is still pending", Revision = state.Revision
		});

		changed.Status.ShouldBe((int)BookCopyStatus.Damaged);
		changed.SectionId.ShouldBe(oldSection);
		changed.ShelfId.ShouldBe(oldShelf);
		var events = await lifecycle.GetEventsAsync(state.BookId, state.CopyId);
		events.Items[0].FromSectionId.ShouldBe(oldSection);
		events.Items[0].FromShelfId.ShouldBe(oldShelf);
		events.Items[0].ToSectionId.ShouldBe(oldSection);
		events.Items[0].ToShelfId.ShouldBe(oldShelf);
	}

	[Fact]
	public async Task A_New_Invalid_Destination_Should_Still_Reject_Before_Any_Event_Is_Written()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var (context, _, _) = await SeedOrphanedLocationAsync();
		var lifecycle = GetRequiredService<ILibraryCopyLifecycleAppService>();
		var state = await lifecycle.GetStateAsync(context.Catalog.BookId, context.Catalog.CopyId);

		await Should.ThrowAsync<BusinessException>(() => lifecycle.MoveAsync(state.BookId, state.CopyId, new()
		{
			SectionId = Guid.NewGuid(), ShelfId = Guid.NewGuid(), Reason = "Requested destination", Revision = state.Revision
		}));
		(await lifecycle.GetEventsAsync(state.BookId, state.CopyId)).TotalCount.ShouldBe(0);
	}

	private Task<(LibraryCopyLifecycleTestContext Context, Guid SectionId, Guid ShelfId)> SeedOrphanedLocationAsync() =>
		WithUnitOfWorkAsync(async () =>
		{
			var context = await LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider);
			var copies = GetRequiredService<IRepository<BookCopy, Guid>>();
			var copy = await copies.GetAsync(context.Catalog.CopyId);
			copy.SectionId = Guid.NewGuid();
			copy.ShelfId = Guid.NewGuid();
			await copies.UpdateAsync(copy, true);
			return (context, copy.SectionId, copy.ShelfId);
		});
}
