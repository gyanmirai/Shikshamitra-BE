using System;
using System.Linq;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Receiving;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryReceivingRegister_Tests : ShikshaMitraApplicationTestBase<LibraryReceivingPreviewTestModule>
{
	[Fact]
	public async Task Receiving_Register_Should_Remain_Readable_Without_The_Add_Action_And_Return_Actual_Recorded_Headers()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		var first = await ReceiveAsync(context, new(2026, 11, 17), LibraryStockSource.Donation, "North donor");
		var second = await ReceiveAsync(context, new(2026, 11, 18), LibraryStockSource.Purchase, "School Press");
		Permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Receiving.Add).Returns(false);

		var result = await App.GetListAsync(new() { PageSize = 1 });

		result.TotalCount.ShouldBe(2); result.TotalPages.ShouldBe(2); result.CurrentPage.ShouldBe(1);
		var row = result.Items.Single(); row.Id.ShouldBe(second.Id); row.LineCount.ShouldBe(1); row.CopyCount.ShouldBe(1);
		row.RecordedBy.ShouldBe(second.RecordedBy); row.RecordedAt.ShouldBe(second.RecordedAt);
		(await App.GetListAsync(new() { Page = 2, PageSize = 1 })).Items.Single().Id.ShouldBe(first.Id);
	}

	[Theory]
	[InlineData(ShikshaMitraSubmodulePermissions.Library.Receiving.View)]
	[InlineData(ShikshaMitraSubmodulePermissions.Library.AllBooks.View)]
	public async Task Receiving_Register_Should_Enforce_Each_Read_Action_For_Direct_Service_Calls(string removed)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		Permissions.IsGrantedAsync(removed).Returns(false);
		await Should.ThrowAsync<AbpAuthorizationException>(() => App.GetListAsync(new()));
	}

	[Fact]
	public async Task Receiving_Register_Should_Order_Same_Day_Receipts_By_The_Actual_Recorded_Instant()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		var first = await ReceiveAsync(context, new(2026, 11, 18), LibraryStockSource.Donation, "First donor");
		var second = await ReceiveAsync(context, new(2026, 11, 18), LibraryStockSource.Donation, "Second donor");
		second.RecordedAt.ShouldBeGreaterThan(first.RecordedAt);
		(await App.GetListAsync(new())).Items.Select(row => row.Id).ShouldBe(new[] { second.Id, first.Id });
	}

	[Fact]
	public async Task Receiving_Register_Should_Filter_School_Before_Counting_And_Paging()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var owned = await ReceiveAsync(await SeedAsync(), new(2026, 11, 17), LibraryStockSource.Donation, "Owned donor");
		using (LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider))
			await ReceiveAsync(await SeedAsync(), new(2026, 11, 18), LibraryStockSource.Donation, "Foreign donor");

		var result = await App.GetListAsync(new() { PageSize = 1 });

		result.TotalCount.ShouldBe(1); result.Items.Single().Id.ShouldBe(owned.Id);
	}

	[Fact]
	public async Task Receiving_Register_Should_Filter_Recorded_Source_Date_Reference_And_Exact_Received_Copy()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		var donation = await ReceiveAsync(context, new(2026, 11, 17), LibraryStockSource.Donation, "North donor");
		var purchase = await ReceiveAsync(context, new(2026, 11, 18), LibraryStockSource.Purchase, "School Press");
		(await App.GetListAsync(new() { Source = LibraryStockSource.Donation })).Items.Single().Id.ShouldBe(donation.Id);
		(await App.GetListAsync(new() { ReceivedFrom = new(2026, 11, 18), ReceivedTo = new(2026, 11, 18) })).Items.Single().Id.ShouldBe(purchase.Id);
		(await App.GetListAsync(new() { Search = " NORTH " })).Items.Single().Id.ShouldBe(donation.Id);
		(await App.GetListAsync(new() { Search = purchase.Reference })).Items.Single().Id.ShouldBe(purchase.Id);
		(await App.GetListAsync(new() { CopyId = donation.Lines.Single().Copies.Single().CopyId })).Items.Single().Id.ShouldBe(donation.Id);
		(await App.GetListAsync(new() { CopyId = context.CopyId })).TotalCount.ShouldBe(0);
	}

	[Fact]
	public async Task Receiving_Register_Should_Keep_An_Extreme_Page_Empty_Without_Overflowing_Into_The_First_Page()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		await ReceiveAsync(await SeedAsync(), new(2026, 11, 18), LibraryStockSource.Donation, "Donor");
		var result = await App.GetListAsync(new() { Page = int.MaxValue, PageSize = 100 });
		result.TotalCount.ShouldBe(1); result.Items.ShouldBeEmpty(); result.CurrentPage.ShouldBe(int.MaxValue);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Receiving_Register_Should_Reject_An_Invalid_Date_Range_Or_Source(bool invalidSource)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		await SeedAsync();
		var input = invalidSource ? new LibraryReceivingListInput { Source = (LibraryStockSource)99 }
			: new LibraryReceivingListInput { ReceivedFrom = new(2026, 11, 18), ReceivedTo = new(2026, 11, 17) };
		var error = await Should.ThrowAsync<BusinessException>(() => App.GetListAsync(input));
		error.Code.ShouldBe(LibraryErrorCodes.ReceivingInvalid);
	}

	private ILibraryReceivingAppService App => GetRequiredService<ILibraryReceivingAppService>();
	private IPermissionChecker Permissions => GetRequiredService<IPermissionChecker>();
	private Task<LibraryPolicyTestContext> SeedAsync() => WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
	private async Task<LibraryStockReceiptDto> ReceiveAsync(LibraryPolicyTestContext context, DateOnly date, LibraryStockSource source, string sourceName)
	{
		var input = new LibraryReceivingInput { ReceivedOn = date, Source = source, SourceName = sourceName, Reference = Guid.NewGuid().ToString("N"),
			Lines = [new() { BookId = context.BookId, SectionId = context.SectionId, ShelfId = context.ShelfId, Quantity = 1 }] };
		var preview = await App.PreviewAsync(input); preview.CanReceive.ShouldBeTrue();
		return await App.ReceiveAsync(new() { Receipt = input, Revision = preview.Revision!, IdempotencyKey = Guid.NewGuid().ToString("N") });
	}
}
