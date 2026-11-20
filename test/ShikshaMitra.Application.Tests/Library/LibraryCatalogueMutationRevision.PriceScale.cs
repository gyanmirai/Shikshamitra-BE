using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Library.Books;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryCatalogueReadAuthority_Tests
{
	[Fact]
	public async Task Catalogue_Revision_Should_Not_Change_For_Equivalent_Decimal_Storage_Scales()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		var books = GetRequiredService<IBookAppService>();
		var opened = await books.GetAsync(context.BookId);
		await WithUnitOfWorkAsync(async () =>
		{
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			// Force the provider's equivalent stored scale. A normal EF update
			// compares decimal values equal and may omit this column entirely.
			(await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"AppBooks\" SET \"Price\"={"100.00"} WHERE \"Id\"={context.BookId}"))
				.ShouldBe(1);
		});
		var stored = await books.GetAsync(context.BookId);
		stored.Price.ShouldBe(opened.Price);
		stored.PriceText.ShouldBe(opened.PriceText);
		stored.MetadataRevision.ShouldBe(opened.MetadataRevision,
			"Equivalent decimal scales must retain a usable accepted-command revision after PostgreSQL normalizes decimal(18,2).");
		await books.UpdateAsync(context.BookId, MetadataEdit(opened, "Reviewed after storage normalization"));
	}
}
