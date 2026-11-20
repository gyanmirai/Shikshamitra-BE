using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Receiving;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using NSubstitute;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryReceivingCommit_Tests : ShikshaMitraApplicationTestBase<LibraryReceivingPreviewTestModule>
{
	[Fact]
	public async Task Receiving_Should_Add_Only_New_Copies_And_Keep_A_Separate_Immutable_Provenance_Record()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync(); var command = await CommandAsync(context);
		var result = await App.ReceiveAsync(command);
		result.ReceivedOn.ShouldBe(command.Receipt.ReceivedOn); result.Source.ShouldBe(LibraryStockSource.Donation);
		result.CopyCount.ShouldBe(2); result.Lines.Single().UnitCost.ShouldBeNull();
		result.Lines.Single().Copies.Select(copy => copy.CopyId).Distinct().Count().ShouldBe(2);
		result.Lines.Single().Copies.ShouldAllBe(copy => copy.CopyId != context.CopyId);
		var stored = await App.GetAsync(result.Id);
		stored.Id.ShouldBe(result.Id); stored.RecordedBy.ShouldNotBe(Guid.Empty); stored.RecordedAt.Offset.ShouldBe(TimeSpan.Zero);
		await WithUnitOfWorkAsync(async () =>
		{
			var stock = await GetRequiredService<IRepository<BookCopy, Guid>>().GetListAsync(row => row.BookId == context.BookId);
			stock.Count.ShouldBe(3); stock.Select(copy => copy.CopyNumber).Distinct().Count().ShouldBe(3);
			stock.Single(copy => copy.Id == context.CopyId).CopyNumber.ShouldBe(1);
			(await GetRequiredService<IRepository<Book, Guid>>().GetAsync(context.BookId)).NumberOfCopies.ShouldBe(3);
			(await GetRequiredService<IRepository<LibraryStockReceipt, Guid>>().CountAsync()).ShouldBe(1);
			(await GetRequiredService<IRepository<LibraryReceivedCopy, Guid>>().CountAsync()).ShouldBe(2);
			(await GetRequiredService<IRepository<LibraryStockReceiptCommand, Guid>>().CountAsync()).ShouldBe(1);
		});
	}

	[Fact]
	public async Task Receiving_Retry_Should_Return_The_Same_Copy_Ids_Without_Receiving_Stock_Again()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync(); var command = await CommandAsync(context);
		var first = await App.ReceiveAsync(command); var second = await App.ReceiveAsync(command);
		second.Id.ShouldBe(first.Id);
		second.Lines.Single().Copies.Select(copy => copy.CopyId).ShouldBe(first.Lines.Single().Copies.Select(copy => copy.CopyId));
		await WithUnitOfWorkAsync(async () =>
		{
			(await GetRequiredService<IRepository<BookCopy, Guid>>().CountAsync(row => row.BookId == context.BookId)).ShouldBe(3);
			(await GetRequiredService<IRepository<LibraryStockReceiptCommand, Guid>>().CountAsync()).ShouldBe(1);
		});
	}

	[Fact]
	public async Task Receiving_Should_Reject_A_Changed_Receipt_Using_An_Already_Accepted_Key()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync(); var command = await CommandAsync(context);
		await App.ReceiveAsync(command); command.Receipt.Lines[0].Quantity = 3;
		await Should.ThrowAsync<BusinessException>(() => App.ReceiveAsync(command));
		await WithUnitOfWorkAsync(async () =>
			(await GetRequiredService<IRepository<BookCopy, Guid>>().CountAsync(row => row.BookId == context.BookId)).ShouldBe(3));
	}

	[Fact]
	public async Task Receiving_Should_Reject_A_Stale_Preview_Before_Any_New_Stock_Or_Receipt_Is_Written()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync(); var command = await CommandAsync(context);
		await WithUnitOfWorkAsync(async () =>
		{
			var book = await GetRequiredService<IRepository<Book, Guid>>().GetAsync(context.BookId);
			book.Title = "Corrected catalogue title";
			await GetRequiredService<IRepository<Book, Guid>>().UpdateAsync(book, true);
		});
		await Should.ThrowAsync<BusinessException>(() => App.ReceiveAsync(command));
		await AssertNoReceivingAsync(context);
	}

	[Fact]
	public async Task Receiving_Should_Validate_A_Bad_Later_Line_Before_Inserting_The_First_Lines_Copies()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync(); var command = await CommandAsync(context);
		command.Receipt.Lines.Add(new() { BookId = context.BookId, SectionId = context.SectionId, ShelfId = Guid.NewGuid(), Quantity = 1 });
		await Should.ThrowAsync<BusinessException>(() => App.ReceiveAsync(command));
		await AssertNoReceivingAsync(context);
	}

	[Fact]
	public async Task Receiving_Replay_Should_Recheck_A_Removed_Receiving_Action_Before_Returning_Accepted_Stock()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync(); var command = await CommandAsync(context);
		await App.ReceiveAsync(command);
		GetRequiredService<IPermissionChecker>().IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Receiving.Add).Returns(false);
		await Should.ThrowAsync<AbpAuthorizationException>(() => App.ReceiveAsync(command));
	}

	private ILibraryReceivingAppService App => GetRequiredService<ILibraryReceivingAppService>();
	private Task<LibraryPolicyTestContext> SeedAsync() => WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
	private async Task<LibraryReceiveCommand> CommandAsync(LibraryPolicyTestContext context)
	{
		var input = new LibraryReceivingInput
		{
			ReceivedOn = new(2026, 11, 18), Source = LibraryStockSource.Donation, SourceName = "Community donor",
			Lines = [new() { BookId = context.BookId, SectionId = context.SectionId, ShelfId = context.ShelfId, Quantity = 2 }]
		};
		var preview = await App.PreviewAsync(input); preview.CanReceive.ShouldBeTrue(); preview.Errors.ShouldBeEmpty();
		return new() { IdempotencyKey = Guid.NewGuid().ToString("N"), Revision = preview.Revision!, Receipt = input };
	}
	private Task AssertNoReceivingAsync(LibraryPolicyTestContext context) => WithUnitOfWorkAsync(async () =>
	{
		(await GetRequiredService<IRepository<BookCopy, Guid>>().CountAsync(row => row.BookId == context.BookId)).ShouldBe(1);
		(await GetRequiredService<IRepository<LibraryStockReceipt, Guid>>().CountAsync()).ShouldBe(0);
		(await GetRequiredService<IRepository<LibraryReceivedCopy, Guid>>().CountAsync()).ShouldBe(0);
		(await GetRequiredService<IRepository<LibraryStockReceiptCommand, Guid>>().CountAsync()).ShouldBe(0);
	});
}
