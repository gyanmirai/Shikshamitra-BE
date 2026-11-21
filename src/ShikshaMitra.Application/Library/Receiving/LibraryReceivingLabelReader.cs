using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Setting.Sections;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Receiving;

public sealed class LibraryReceivingLabelReader(IRepository<Book, Guid> books,
	IRepository<LibrarySection, Guid> sections, IRepository<LibraryShelf, Guid> shelves,
	IAsyncQueryableExecuter queries, ICurrentTenant tenant) : ITransientDependency
{
	internal async Task PopulateAsync(List<LibraryStockReceiptLineDto> lines)
	{
		var bookIds = lines.Select(line => line.BookId).Distinct().ToArray();
		var sectionIds = lines.Select(line => line.SectionId).Distinct().ToArray();
		var shelfIds = lines.Select(line => line.ShelfId).Distinct().ToArray();
		var titles = await queries.ToListAsync((await books.GetQueryableAsync())
			.Where(book => book.TenantId == tenant.Id && Enumerable.Contains(bookIds, book.Id)).Select(book => new { book.Id, book.Title }));
		var sectionQuery = (await sections.GetQueryableAsync()).Where(section => section.TenantId == tenant.Id);
		var sectionRows = await queries.ToListAsync(sectionQuery.Where(section => Enumerable.Contains(sectionIds, section.Id))
			.Select(section => new { section.Id, section.SectionName }));
		var shelfQuery = (await shelves.GetQueryableAsync()).Where(shelf => Enumerable.Contains(shelfIds, shelf.Id));
		var shelfRows = await queries.ToListAsync(from shelf in shelfQuery
			join section in sectionQuery on shelf.SectionId equals section.Id select new { shelf.Id, shelf.ShelfName });
		var titleById = titles.ToDictionary(book => book.Id, book => book.Title);
		var sectionById = sectionRows.ToDictionary(section => section.Id, section => section.SectionName);
		var shelfById = shelfRows.ToDictionary(shelf => shelf.Id, shelf => shelf.ShelfName);
		foreach (var line in lines)
		{
			line.CurrentBookTitle = titleById.GetValueOrDefault(line.BookId);
			line.CurrentSectionName = sectionById.GetValueOrDefault(line.SectionId);
			line.CurrentShelfName = shelfById.GetValueOrDefault(line.ShelfId);
		}
	}
}
