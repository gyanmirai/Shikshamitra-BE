using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;
using ShikshaMitra.Library.Setting.Sections;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryPersistentCatalog_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Fact]
	public async Task Current_Year_Context_Should_Show_Existing_Physical_Holdings_Without_Cloning()
	{
		var context = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		var result = await GetRequiredService<IBookAppService>().GetListAsync(context.YearId);

		result.Items.Single().Id.ShouldBe(context.BookId);
		result.Items.Single().NumberOfCopies.ShouldBe(1);
		await WithUnitOfWorkAsync(async () =>
		{
			(await GetRequiredService<IRepository<BookCopy, Guid>>().GetListAsync(x => x.BookId == context.BookId)).Single().Id.ShouldBe(context.CopyId);
			(await GetRequiredService<IRepository<Book, Guid>>().GetAsync(context.BookId)).AcademicYearId.ShouldBe(context.OriginYearId);
		});
	}

	[Fact]
	public async Task Current_Year_Context_Should_Show_Existing_Categories_And_Sections()
	{
		var context = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));

		var categories = await GetRequiredService<IBookCategoryAppService>().GetListAsync(context.YearId);
		var sections = await GetRequiredService<ILibrarySectionAppService>().GetListAsync(context.YearId);

		categories.Items.Select(x => x.Id).ShouldContain(context.CategoryId);
		sections.Items.Select(x => x.Id).ShouldContain(context.SectionId);
	}

	[Fact]
	public async Task New_Catalog_Record_Should_Reuse_Existing_Category_And_Location_In_Current_Context()
	{
		var context = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		var created = await GetRequiredService<IBookAppService>().CreateAsync(context.YearId, new CreateBookRequest
		{
			Title = "New purchase", Author = "School Author", CategoryId = context.CategoryId, Language = Language.English,
			Copies = [new() { CopyNumber = 1, SectionId = context.SectionId, ShelfId = context.ShelfId }]
		});

		(await GetRequiredService<IBookAppService>().GetCopiesAsync(Guid.Parse(created.Id))).TotalCount.ShouldBe(1);
	}

	[Fact]
	public async Task Catalog_Context_Should_Reject_Unknown_Academic_Year()
	{
		await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));

		await Should.ThrowAsync<BusinessException>(() => GetRequiredService<IBookAppService>().GetListAsync(Guid.NewGuid()));
	}
}
