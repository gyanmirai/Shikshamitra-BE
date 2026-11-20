using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Receiving;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryReceivingCatalogueRetention_Tests
{
	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task Catalogue_Deletion_Eligibility_Should_Use_The_Delete_Action_Independently_Of_Copy_Update(bool deleteGranted)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		var permissions = GetRequiredService<IPermissionChecker>();
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.Update).Returns(false);
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.Delete).Returns(deleteGranted);
		var books = GetRequiredService<IBookAppService>();
		var detail = await books.GetAsync(context.BookId);
		detail.Copies.Single().CanRemove.ShouldBeFalse();
		var json = JsonSerializer.SerializeToElement(detail);
		json.TryGetProperty("CanDelete", out var allowed).ShouldBeTrue();
		allowed.GetBoolean().ShouldBe(deleteGranted);
		if (deleteGranted)
		{
			json.GetProperty("DeletionBlockedReason").ValueKind.ShouldBe(JsonValueKind.Null);
			await books.DeleteAsync(context.BookId);
			await WithUnitOfWorkAsync(async () => (await GetRequiredService<IRepository<Book, Guid>>().FindAsync(context.BookId)).ShouldBeNull());
		}
		else json.GetProperty("DeletionBlockedReason").GetString().ShouldNotBeNullOrWhiteSpace();
	}

	[Theory]
	[InlineData("Loan")]
	[InlineData("Reservation")]
	[InlineData("Receiving")]
	public async Task Catalogue_Deletion_Eligibility_Should_Match_The_Actual_Command_For_Retained_Title_History(string history)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		var tenant = GetRequiredService<ICurrentTenant>().Id;
		if (history == "Receiving")
		{
			var receiving = GetRequiredService<ILibraryReceivingAppService>();
			var input = new LibraryReceivingInput { ReceivedOn = new(2026, 11, 18), Source = LibraryStockSource.Donation,
				Lines = [new() { BookId = context.BookId, SectionId = context.SectionId, ShelfId = context.ShelfId, Quantity = 1 }] };
			var preview = await receiving.PreviewAsync(input); preview.CanReceive.ShouldBeTrue();
			await receiving.ReceiveAsync(new() { Receipt = input, Revision = preview.Revision!, IdempotencyKey = Guid.NewGuid().ToString("N") });
		}
		else await WithUnitOfWorkAsync(async () =>
		{
			if (history == "Loan")
			{
				var copy = await GetRequiredService<IRepository<BookCopy, Guid>>().GetAsync(context.CopyId);
				await GetRequiredService<IRepository<BookIssue, Guid>>().InsertAsync(new(Guid.NewGuid(), copy.Id, copy.AccessionNumber,
					Guid.NewGuid(), 0, new(2025, 4, 1), new(2025, 4, 8), tenant) { Status = BookIssueStatus.Returned, ReturnDate = new(2025, 4, 7) }, true);
			}
			else await GetRequiredService<IRepository<BookReservation, Guid>>().InsertAsync(new(Guid.NewGuid(), context.BookId,
				Guid.NewGuid(), new(2025, 4, 1), new(2025, 4, 8), tenant), true);
		});
		var books = GetRequiredService<IBookAppService>();
		var detail = await books.GetAsync(context.BookId);
		var json = JsonSerializer.SerializeToElement(detail);
		json.TryGetProperty("CanDelete", out var allowed).ShouldBeTrue();
		allowed.GetBoolean().ShouldBeFalse(); json.GetProperty("DeletionBlockedReason").GetString().ShouldNotBeNullOrWhiteSpace();
		var rejected = await Should.ThrowAsync<BusinessException>(() => books.DeleteAsync(context.BookId));
		rejected.Code.ShouldBe(LibraryErrorCodes.CopyHasHistory);
		(await books.GetAsync(context.BookId)).Id.ShouldBe(context.BookId);
	}
}
