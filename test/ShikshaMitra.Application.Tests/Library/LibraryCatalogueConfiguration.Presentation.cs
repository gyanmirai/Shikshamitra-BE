using System;
using System.Linq;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryCatalogueConfiguration_Tests
{
	[Fact]
	public async Task Configured_Custom_Language_And_Material_Should_Be_Truthful_In_Detail_And_Catalogue_List()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		var definition = Definition(); definition.RequiredFields.Remove("author");
		definition.Languages.Add(new() { Code = "bn", Label = "বাংলা" });
		definition.Materials.Add(new() { Code = "picture-book", Label = "Picture book" });
		var saved = await Configuration.UpdateAsync(new() { ExpectedRevision = "defaults-v1", Definition = definition });
		var input = Input(context, saved.Revision);
		input.Author = null; input.Language = null; input.LanguageCode = "bn"; input.MaterialCode = "picture-book";
		var created = await Books.CreateAsync(context.YearId, input);
		var id = Guid.Parse(created.Id);
		var detail = await Books.GetAsync(id);
		detail.Author.ShouldBeEmpty(); detail.Language.ShouldBeNull();
		detail.LanguageCode.ShouldBe("bn"); detail.LanguageLabel.ShouldBe("বাংলা");
		detail.MaterialCode.ShouldBe("picture-book"); detail.MaterialLabel.ShouldBe("Picture book");
		var row = (await Books.GetListAsync(context.YearId)).Items.Single(book => book.Id == id);
		row.Language.ShouldBeNull(); row.LanguageCode.ShouldBe("bn"); row.LanguageLabel.ShouldBe("বাংলা");
		row.MaterialCode.ShouldBe("picture-book"); row.MaterialLabel.ShouldBe("Picture book");
	}

	[Fact]
	public async Task Catalogue_View_Alone_Should_Display_The_Existing_Gujarati_Zero_Without_Settings_Or_Editor_Grants()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<Book, Guid>>();
			var existing = await repository.GetAsync(context.BookId); existing.Language = (Language)0;
			await repository.UpdateAsync(existing, true);
		});
		Permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Settings_CatalogueConfiguration.View).Returns(false);
		Permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Settings_CatalogueConfiguration.Update).Returns(false);
		Permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.Add).Returns(false);
		Permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.Update).Returns(false);
		var detail = await Books.GetAsync(context.BookId);
		detail.Language.ShouldBe(0); detail.LanguageCode.ShouldBe("gu"); detail.LanguageLabel.ShouldBe("Gujarati");
		detail.MaterialCode.ShouldBeNull(); detail.MaterialLabel.ShouldBeNull();
		var row = (await Books.GetListAsync(context.YearId)).Items.Single(book => book.Id == context.BookId);
		row.Language.ShouldBe(0); row.LanguageCode.ShouldBe("gu"); row.LanguageLabel.ShouldBe("Gujarati");
	}
}
