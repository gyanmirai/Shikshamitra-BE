using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryCatalogueReadAuthority_Tests
{
	[Fact]
	public async Task Catalogue_Revision_Should_Reject_A_Stale_Metadata_Edit_And_Retain_The_Accepted_Description()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		var books = GetRequiredService<IBookAppService>();
		var first = await books.GetAsync(context.BookId);
		var older = await books.GetAsync(context.BookId);
		await books.UpdateAsync(context.BookId, MetadataEdit(first, "Accepted description"));
		var error = await Should.ThrowAsync<BusinessException>(() => books.UpdateAsync(context.BookId, MetadataEdit(older, "Stale description")));
		error.Code.ShouldBe("Library:Catalogue:010088");
		(await books.GetAsync(context.BookId)).Title.ShouldBe("Accepted description");
	}

	[Fact]
	public async Task Catalogue_Revision_Should_Stop_An_Older_Complete_Copy_List_From_Removing_Another_Operators_New_Copy()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		var books = GetRequiredService<IBookAppService>();
		var opened = await books.GetAsync(context.BookId);
		var addition = CopiesEdit(opened);
		addition.Copies.Add(new() { CopyNumber = 2, SectionId = context.SectionId, ShelfId = context.ShelfId });
		await books.PutCopiesAsync(context.BookId, addition);
		var error = await Should.ThrowAsync<BusinessException>(() => books.PutCopiesAsync(context.BookId, CopiesEdit(opened)));
		error.Code.ShouldBe("Library:Catalogue:010088");
		var remaining = await books.GetCopiesAsync(context.BookId);
		remaining.TotalCount.ShouldBe(2);
		remaining.Items.Select(copy => copy.Id).ShouldContain(context.CopyId);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task Catalogue_Revision_Should_Reject_An_Unreviewed_Missing_Revision_Before_Writing(bool metadata)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		var books = GetRequiredService<IBookAppService>();
		var opened = await books.GetAsync(context.BookId);
		var error = await Should.ThrowAsync<BusinessException>(async () =>
		{
			if (metadata) await books.UpdateAsync(context.BookId, MetadataEdit(opened, "Unreviewed", missing: true));
			else await books.PutCopiesAsync(context.BookId, CopiesEdit(opened, missing: true));
		});
		error.Code.ShouldBe("Library:Catalogue:010088");
		(await books.GetAsync(context.BookId)).Title.ShouldBe(opened.Title);
	}

	[Fact]
	public async Task Catalogue_Revision_Should_Return_The_Accepted_Command_Revisions_Without_Adopting_A_Later_Read()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		var books = GetRequiredService<IBookAppService>();
		var opened = await books.GetAsync(context.BookId);
		var result = await books.UpdateAsync(context.BookId, MetadataEdit(opened, "Reviewed metadata"));
		var current = await books.GetAsync(context.BookId);
		RequiredRevision(result, "MetadataRevision").ShouldBe(RequiredRevision(current, "MetadataRevision"));
		RequiredRevision(current, "MetadataRevision").ShouldNotBe(RequiredRevision(opened, "MetadataRevision"));
		RequiredRevision(current, "CopiesRevision").ShouldBe(RequiredRevision(opened, "CopiesRevision"));
		var copyEdit = CopiesEdit(opened);
		copyEdit.Copies[0].CopyNumber = 2;
		var copyResult = await books.PutCopiesAsync(context.BookId, copyEdit);
		current = await books.GetAsync(context.BookId);
		RequiredRevision(copyResult, "CopiesRevision").ShouldBe(RequiredRevision(current, "CopiesRevision"));
		RequiredRevision(current, "CopiesRevision").ShouldNotBe(RequiredRevision(opened, "CopiesRevision"));
	}
}
