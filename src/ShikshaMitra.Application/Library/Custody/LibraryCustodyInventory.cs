using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Reservations;
using ShikshaMitra.Library.Setting.Sections;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Library.Custody;

internal sealed record LibraryCustodyStock(BookCopy Copy, string Title, long EventSequence)
{
	public LibraryCustodyCopyOptionDto Label => new(Copy.Id, Copy.BookId, Title, Copy.AccessionNumber, Copy.Status);
}

public class LibraryCustodyInventory(IRepository<BookCopy, Guid> copies, IRepository<Book, Guid> books,
	IRepository<BookIssue, Guid> loans, IRepository<LibraryHold, Guid> holds, IRepository<LibraryCustodyItem, Guid> items,
	IRepository<LibraryCopyEvent, Guid> events, IRepository<LibrarySection, Guid> sections, IRepository<LibraryShelf, Guid> shelves,
	IAsyncQueryableExecuter queries) : ITransientDependency
{
	internal async Task<List<LibraryCustodyCopyOptionDto>> SearchAsync(Guid tenant, string? query, Guid? sectionId = null)
	{
		var term = query?.Trim().ToUpperInvariant();
		if (term == null || term.Length is < 1 or > 100) return [];
		var inventory = await copies.GetQueryableAsync();
		var titles = await books.GetQueryableAsync();
		var result = from copy in inventory
			join book in titles on copy.BookId equals book.Id
			where copy.TenantId == tenant && book.TenantId == tenant && copy.Status == BookCopyStatus.Available &&
				(!sectionId.HasValue || copy.SectionId == sectionId.Value) &&
				(copy.NormalizedAccessionNumber.Contains(term) || book.Title.ToUpper().Contains(term))
			orderby copy.NormalizedAccessionNumber == term descending, book.Title, copy.AccessionNumber, copy.Id
			select new LibraryCustodyCopyOptionDto(copy.Id, book.Id, book.Title, copy.AccessionNumber, copy.Status);
		return await queries.ToListAsync(result.Take(30));
	}

	internal async Task<Dictionary<Guid, LibraryCustodyStock>> LoadAsync(Guid tenant, Guid[] copyIds)
	{
		var inventory = await copies.GetQueryableAsync();
		var titles = await books.GetQueryableAsync();
		var homeSections = await sections.GetQueryableAsync();
		var homeShelves = await shelves.GetQueryableAsync();
		var stock = await queries.ToListAsync(from copy in inventory
			join book in titles on copy.BookId equals book.Id
			join section in homeSections on copy.SectionId equals section.Id
			join shelf in homeShelves on copy.ShelfId equals shelf.Id
			where copy.TenantId == tenant && book.TenantId == tenant && section.TenantId == tenant &&
				shelf.SectionId == section.Id && Enumerable.Contains(copyIds, copy.Id)
			select new { Copy = copy, book.Title });
		var log = await queries.ToListAsync((await events.GetQueryableAsync())
			.Where(row => row.TenantId == tenant && Enumerable.Contains(copyIds, row.CopyId))
			.GroupBy(row => row.CopyId).Select(group => new { CopyId = group.Key, Sequence = group.Max(row => row.Sequence) }));
		var sequences = log.ToDictionary(row => row.CopyId, row => row.Sequence);
		return stock.ToDictionary(row => row.Copy.Id, row => new LibraryCustodyStock(row.Copy, row.Title,
			sequences.GetValueOrDefault(row.Copy.Id)));
	}

	internal async Task<HashSet<Guid>> FindUnavailableAsync(Guid tenant, Dictionary<Guid, LibraryCustodyStock> stock, bool handingOff)
	{
		var ids = stock.Keys.ToArray();
		var accessions = stock.Values.Select(row => row.Copy.NormalizedAccessionNumber).Distinct().ToArray();
		var issued = await queries.ToListAsync((await loans.GetQueryableAsync()).Where(row => row.TenantId == tenant &&
			row.Status != BookIssueStatus.Returned && (Enumerable.Contains(ids, row.BookCopyId) ||
				Enumerable.Contains(accessions, row.AccessionNumber.Trim().ToUpper())))
			.Select(row => new { row.BookCopyId, Accession = row.AccessionNumber.Trim().ToUpper() }));
		var issuedCopyIds = issued.Select(row => row.BookCopyId).ToHashSet();
		var issuedAccessions = issued.Select(row => row.Accession).ToHashSet(StringComparer.Ordinal);
		var allocated = await queries.ToListAsync((await holds.GetQueryableAsync()).Where(row => row.TenantId == tenant &&
			row.State == LibraryHoldState.Allocated && Enumerable.Contains(ids, row.CopyId)).Select(row => row.CopyId));
		var unavailable = stock.Values.Where(row => issuedCopyIds.Contains(row.Copy.Id) ||
			issuedAccessions.Contains(row.Copy.NormalizedAccessionNumber)).Select(row => row.Copy.Id).Concat(allocated).ToHashSet();
		if (handingOff)
			unavailable.UnionWith(await queries.ToListAsync((await items.GetQueryableAsync()).Where(row => row.TenantId == tenant &&
				!row.ReturnReceiptId.HasValue && Enumerable.Contains(ids, row.CopyId)).Select(row => row.CopyId)));
		return unavailable;
	}
}
