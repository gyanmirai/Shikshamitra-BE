using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Setting.Sections;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Library.Receiving;

public sealed class LibraryReceivingPreviewBuilder(IRepository<Book, Guid> books, IRepository<BookCopy, Guid> copies,
	IRepository<LibrarySection, Guid> sections, IRepository<LibraryShelf, Guid> shelves,
	IAsyncQueryableExecuter asyncExecuter) : ITransientDependency
{
	public async Task<LibraryReceivingPreviewDto> BuildAsync(Guid tenantId, LibraryReceivingInput input)
	{
		var result = new LibraryReceivingPreviewDto();
		if (input == null || input.Lines == null || input.Lines.Count is < 1 or > LibraryStockReceiptRules.MaximumLines ||
			input.Lines.Any(line => line == null))
		{
			Error(result, null, "InvalidLines", "Choose between 1 and 100 stock lines.");
			return result;
		}
		if (!LibraryReceivingInputRules.ValidHeader(input))
			Error(result, null, "InvalidHeader", "Choose an explicit receipt date and source, with source and reference details no longer than 256 characters.");
		if (input.Lines.Sum(line => (long)Math.Max(0, line.Quantity)) > LibraryStockReceiptRules.MaximumCopies ||
			input.Lines.Sum(line => (long)(line.Accessions?.Count ?? 0)) > LibraryStockReceiptRules.MaximumCopies)
		{
			Error(result, null, "CopyLimit", "Receive at most 5,000 copies in one batch.");
			return result;
		}

		var bookIds = input.Lines.Select(line => line.BookId).Distinct().ToArray();
		var sectionIds = input.Lines.Select(line => line.SectionId).Distinct().ToArray();
		var shelfIds = input.Lines.Select(line => line.ShelfId).Distinct().ToArray();
		var bookRows = await asyncExecuter.ToListAsync((await books.GetQueryableAsync()).Where(book => book.TenantId == tenantId &&
			Enumerable.Contains(bookIds, book.Id)).Select(book => new { book.Id, book.Title, book.ConcurrencyStamp }));
		var sectionRows = await asyncExecuter.ToListAsync((await sections.GetQueryableAsync()).Where(section => section.TenantId == tenantId &&
			Enumerable.Contains(sectionIds, section.Id)).Select(section => new { section.Id, section.SectionName, section.ConcurrencyStamp }));
		var ownedSectionIds = sectionRows.Select(section => section.Id).ToArray();
		var shelfRows = await asyncExecuter.ToListAsync((await shelves.GetQueryableAsync()).Where(shelf =>
			Enumerable.Contains(ownedSectionIds, shelf.SectionId) && Enumerable.Contains(shelfIds, shelf.Id))
			.Select(shelf => new { shelf.Id, shelf.SectionId, shelf.ShelfName }));
		var stock = await asyncExecuter.ToListAsync((await copies.GetQueryableAsync()).Where(copy => copy.TenantId == tenantId &&
			Enumerable.Contains(bookIds, copy.BookId)).GroupBy(copy => copy.BookId)
			.Select(group => new { BookId = group.Key, Count = group.Count(), MaximumCopyNumber = group.Max(copy => copy.CopyNumber) }));
		var manual = input.Lines.Where(line => line.Accessions != null).SelectMany(line => line.Accessions!)
			.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim().ToUpperInvariant()).Distinct().ToArray();
		var occupied = manual.Length == 0 ? [] : await asyncExecuter.ToListAsync((await copies.GetQueryableAsync())
			.Where(copy => copy.TenantId == tenantId && Enumerable.Contains(manual, copy.NormalizedAccessionNumber))
			.Select(copy => copy.NormalizedAccessionNumber));
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var totals = new Dictionary<string, decimal>(StringComparer.Ordinal);
		for (var index = 0; index < input.Lines.Count; index++)
		{
			var line = input.Lines[index];
			var book = bookRows.SingleOrDefault(row => row.Id == line.BookId);
			var section = sectionRows.SingleOrDefault(row => row.Id == line.SectionId);
			var shelf = shelfRows.SingleOrDefault(row => row.Id == line.ShelfId && row.SectionId == line.SectionId);
			if (book == null || section == null || shelf == null)
			{
				Error(result, index, "UnavailableLocation", "Choose an available school title, section and a shelf in that section.");
				continue;
			}
			if (line.Quantity is < 1 or > LibraryStockReceiptRules.MaximumCopies ||
				!LibraryReceivingInputRules.TryCost(line.UnitCost, line.Currency, out var cost))
			{
				Error(result, index, "InvalidQuantityOrCost", "Enter a positive quantity and an exact nonnegative cost with its three-letter currency, or leave both cost and currency unknown.");
				continue;
			}
			if (line.Accessions != null && (line.Accessions.Count != line.Quantity || line.Accessions.Any(value =>
				!string.IsNullOrWhiteSpace(value) && (value.Length > 50 || value.Any(char.IsControl) ||
				!seen.Add(value.Trim()) || occupied.Contains(value.Trim().ToUpperInvariant())))))
			{
				Error(result, index, "AccessionConflict", "Provide one unique accession per copy, leaving automatic values blank. Existing accessions cannot be received again.");
				continue;
			}
			result.Lines.Add(new()
			{
				LineIndex = index, BookId = line.BookId, BookTitle = book.Title, SectionId = line.SectionId,
				SectionName = section.SectionName ?? string.Empty, ShelfId = line.ShelfId, ShelfName = shelf.ShelfName ?? string.Empty,
				Quantity = line.Quantity, UnitCost = LibraryReceivingInputRules.CostText(cost), Currency = line.Currency
			});
			if (cost.HasValue)
			{
				result.KnownCostCopyCount += line.Quantity;
				totals[line.Currency!] = totals.GetValueOrDefault(line.Currency!) + cost.Value * line.Quantity;
			}
			else result.UnknownCostCopyCount += line.Quantity;
		}
		result.CanReceive = result.Errors.Count == 0;
		if (result.CanReceive)
		{
			result.KnownTotals = totals.OrderBy(pair => pair.Key).Select(pair => new LibraryReceivingCurrencyTotalDto
				{ Currency = pair.Key, Amount = LibraryReceivingInputRules.CostText(pair.Value)! }).ToList();
			var evidence = JsonSerializer.Serialize(new { Receipt = LibraryReceivingInputRules.Normalize(input),
				Books = bookRows.OrderBy(row => row.Id), Sections = sectionRows.OrderBy(row => row.Id),
				Shelves = shelfRows.OrderBy(row => row.Id), Stock = stock.OrderBy(row => row.BookId) });
			result.Revision = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(evidence)));
		}
		return result;
	}

	private static void Error(LibraryReceivingPreviewDto result, int? index, string code, string message) =>
		result.Errors.Add(new() { LineIndex = index, Code = code, Message = message });
}
