using System;
using System.Linq;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Receiving;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryReceivingCatalogueRetention_Tests : ShikshaMitraApplicationTestBase<LibraryReceivingPreviewTestModule>
{
	public LibraryReceivingCatalogueRetention_Tests()
	{
		var permissions = GetRequiredService<IPermissionChecker>();
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.Delete).Returns(true);
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.Update).Returns(true);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task Receiving_Catalogue_Should_Explain_Why_Received_Copies_Cannot_Be_Removed(bool deleteTitle)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		var receiving = GetRequiredService<ILibraryReceivingAppService>();
		var input = new LibraryReceivingInput
		{
			ReceivedOn = new(2026, 11, 18), Source = LibraryStockSource.Donation,
			Lines = [new() { BookId = context.BookId, SectionId = context.SectionId, ShelfId = context.ShelfId, Quantity = 1 }]
		};
		var preview = await receiving.PreviewAsync(input); preview.CanReceive.ShouldBeTrue();
		var receipt = await receiving.ReceiveAsync(new()
			{ Receipt = input, Revision = preview.Revision!, IdempotencyKey = Guid.NewGuid().ToString("N") });
		var oldCopy = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<BookCopy, Guid>>().GetAsync(context.CopyId));
		var catalogue = GetRequiredService<IBookAppService>();
		var opened = await catalogue.GetAsync(context.BookId);

		var error = await Should.ThrowAsync<BusinessException>(async () =>
		{
			if (deleteTitle) await catalogue.DeleteAsync(context.BookId);
			else await catalogue.PutCopiesAsync(context.BookId, new()
			{
				ExpectedCopiesRevision = opened.CopiesRevision,
				Copies = [new() { CopyId = oldCopy.Id, CopyNumber = oldCopy.CopyNumber, AccessionNumber = oldCopy.AccessionNumber,
					SectionId = oldCopy.SectionId, ShelfId = oldCopy.ShelfId }]
			});
		});

		error.Code.ShouldBe(LibraryErrorCodes.CopyHasHistory);
		(await receiving.GetAsync(receipt.Id)).Lines.Single().Copies.Single().CopyId
			.ShouldBe(receipt.Lines.Single().Copies.Single().CopyId);
		await WithUnitOfWorkAsync(async () =>
			(await GetRequiredService<IRepository<BookCopy, Guid>>().CountAsync(copy => copy.BookId == context.BookId)).ShouldBe(2));
	}
}
