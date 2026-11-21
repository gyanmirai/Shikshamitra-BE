using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Receiving;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryReceivingEvidenceIntegrity_Tests : ShikshaMitraApplicationTestBase<LibraryReceivingPreviewTestModule>
{
	[Fact]
	public async Task Complete_Receiving_Evidence_Should_Save_The_Actual_Header_Line_Copy_And_Command_Together()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var catalog = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		var receipt = await WithUnitOfWorkAsync(() => AddAsync(catalog));
		await WithUnitOfWorkAsync(async () =>
		{
			(await GetRequiredService<IRepository<LibraryStockReceipt, Guid>>().CountAsync()).ShouldBe(1);
			(await GetRequiredService<IRepository<LibraryStockReceiptLine, Guid>>().CountAsync()).ShouldBe(1);
			(await GetRequiredService<IRepository<LibraryReceivedCopy, Guid>>().CountAsync()).ShouldBe(1);
			(await GetRequiredService<IRepository<LibraryStockReceiptCommand, Guid>>().CountAsync()).ShouldBe(1);
		});
		receipt.ExpectedCopyCount.ShouldBe(1);
	}

	[Theory]
	[InlineData("MissingCommand")]
	[InlineData("DifferentCommandActor")]
	[InlineData("IncompleteCopySet")]
	public async Task An_Incomplete_Or_Contradictory_Receiving_Set_Should_Be_Rejected_Before_Commit(string defect)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var catalog = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		var error = await Record.ExceptionAsync(() => WithUnitOfWorkAsync(() => AddAsync(catalog, defect)));
		error.ShouldNotBeNull("a receiving receipt must seal its complete copy set and actual accepted command");
	}

	[Fact]
	public async Task A_Received_Copy_Link_Cannot_Be_Deleted_While_Its_Receipt_Continues_To_Claim_The_Copy()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var catalog = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		var receipt = await WithUnitOfWorkAsync(() => AddAsync(catalog));
		var copyId = receipt.Lines.Single().Copies.Single().Id;
		var error = await Record.ExceptionAsync(() => WithUnitOfWorkAsync(() =>
			GetRequiredService<IRepository<LibraryReceivedCopy, Guid>>().DeleteAsync(copyId, true)));
		error.ShouldNotBeNull("removing a child must not silently erase committed acquisition provenance");
	}

	[Fact]
	public async Task A_Later_Copy_Link_Cannot_Be_Appended_To_A_Previously_Sealed_Receiving_Line()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var catalog = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		var receipt = await WithUnitOfWorkAsync(() => AddAsync(catalog));
		var error = await Record.ExceptionAsync(() => WithUnitOfWorkAsync(async () =>
		{
			var tenant = GetRequiredService<ICurrentTenant>().Id!.Value;
			var copy = new BookCopy(Guid.NewGuid(), catalog.BookId, 3, $"REC-{Guid.NewGuid():N}", catalog.SectionId, catalog.ShelfId, tenant);
			await GetRequiredService<IRepository<BookCopy, Guid>>().InsertAsync(copy);
			var forged = Receipt(catalog, tenant, copy, receipt.Lines.Single().Id);
			await GetRequiredService<IRepository<LibraryReceivedCopy, Guid>>().InsertAsync(forged.Lines.Single().Copies.Single(), true);
		}));
		error.ShouldNotBeNull("new stock needs its own new receiving record; an old receipt is not an appendable container");
	}

	private async Task<LibraryStockReceipt> AddAsync(LibraryPolicyTestContext catalog, string? defect = null)
	{
		var tenant = GetRequiredService<ICurrentTenant>().Id!.Value;
		var copy = new BookCopy(Guid.NewGuid(), catalog.BookId, 2, $"REC-{Guid.NewGuid():N}", catalog.SectionId, catalog.ShelfId, tenant);
		await GetRequiredService<IRepository<BookCopy, Guid>>().InsertAsync(copy);
		var receipt = Receipt(catalog, tenant, copy, Guid.NewGuid());
		if (defect == "IncompleteCopySet")
			typeof(LibraryStockReceiptLine).GetProperty(nameof(LibraryStockReceiptLine.ExpectedCopyCount))!.SetValue(receipt.Lines.Single(), 2);
		await GetRequiredService<IRepository<LibraryStockReceipt, Guid>>().InsertAsync(receipt);
		if (defect != "MissingCommand")
			await GetRequiredService<IRepository<LibraryStockReceiptCommand, Guid>>().InsertAsync(new(Guid.NewGuid(), tenant,
				defect == "DifferentCommandActor" ? Guid.NewGuid() : receipt.RecordedBy, receipt.Id,
				Guid.NewGuid().ToString("N"), new('A', 64), receipt.RecordedAt));
		return receipt;
	}
	private LibraryStockReceipt Receipt(LibraryPolicyTestContext catalog, Guid tenant, BookCopy copy, Guid lineId) => new(
		Guid.NewGuid(), tenant, new(2026, 11, 18), LibraryStockSource.Donation, "Community donor", null,
		GetRequiredService<ICurrentUser>().Id!.Value, DateTimeOffset.UtcNow,
		[new(lineId, catalog.BookId, catalog.SectionId, catalog.ShelfId, 1, null, null,
			[new(copy.Id, copy.AccessionNumber, copy.CopyNumber)])]);
}
