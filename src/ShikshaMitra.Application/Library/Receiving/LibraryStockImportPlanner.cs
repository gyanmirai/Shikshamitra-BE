using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Catalogue;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Library.Receiving;

internal sealed record LibraryStockImportPlan(LibraryStockImportPreviewDto Preview, List<LibraryImportRow> Rows,
	Dictionary<Guid, string> ExistingTitles, string ConfigurationRevision);

public sealed class LibraryStockImportPlanner(LibraryCatalogueConfigurationReader catalogue, LibraryStockImportLocations locations,
	IRepository<Book, Guid> books, IRepository<BookCopy, Guid> copies, IRepository<LibraryStockImportRow, Guid> accepted,
	IAsyncQueryableExecuter queries) : ITransientDependency
{
	internal async Task<LibraryStockImportPlan> BuildAsync(Guid tenant, LibraryStockImportInput input, bool legacy, bool canCreate, string payload)
	{
		var result = new LibraryStockImportPreviewDto();
		var rows = LibraryStockImportCsv.Parse(input, result.Errors);
		var newRows = rows.Where(row => row.NewTitle != null).ToList();
		if (newRows.Count > 0 && !canCreate) throw new AbpAuthorizationException();
		var configuration = await catalogue.ReadAsync();
		var batchHeader = new LibraryReceivingInput
			{ ReceivedOn = input.ReceivedOn, Source = input.Source, SourceName = CleanSource(input.SourceName) };
		if (!LibraryReceivingInputRules.ValidHeader(batchHeader) || batchHeader.SourceName?.Any(char.IsControl) == true)
			result.Errors.Add(new(null, "Receipt", "InvalidReceipt", "Choose a receipt date and stock source, with a source name up to 256 characters."));
		var position = await locations.FindAsync(tenant, input, legacy);
		if (position == null)
			result.Errors.Add(new(null, "ShelfId", "InvalidLocation", "Choose a shelf in a section available for the selected academic year."));
		var category = newRows.Count == 0 ? null : await locations.FindCategoryAsync(tenant, input.AcademicYearId, legacy, input.CategoryId);
		if (newRows.Count > 0 && category == null)
			result.Errors.Add(new(null, "CategoryId", "InvalidCategory", "Choose an available category for new catalogue titles."));

		var ids = rows.Where(row => row.BookId.HasValue).Select(row => row.BookId!.Value).Distinct().ToArray();
		var existing = await queries.ToListAsync((await books.GetQueryableAsync())
			.Where(book => book.TenantId == tenant && Enumerable.Contains(ids, book.Id))
			.Select(book => new { book.Id, book.Title, book.ConcurrencyStamp }));
		var existingNames = existing.ToDictionary(book => book.Id, book => book.Title);
		foreach (var row in rows.Where(row => row.BookId.HasValue && !existingNames.ContainsKey(row.BookId.Value)))
			result.Errors.Add(new(row.Number, "BookId", "UnavailableBook", "Choose a catalogue title registered at this school."));

		var stock = await queries.ToListAsync((await copies.GetQueryableAsync())
			.Where(copy => copy.TenantId == tenant && Enumerable.Contains(ids, copy.BookId)).GroupBy(copy => copy.BookId)
			.Select(group => new { BookId = group.Key, Count = group.Count(), HighestNumber = group.Max(row => row.CopyNumber) }));
		var stockNumbers = stock.ToDictionary(row => row.BookId, row => row.HighestNumber);
		foreach (var group in rows.Where(row => row.BookId.HasValue).GroupBy(row => row.BookId!.Value))
			if ((long)stockNumbers.GetValueOrDefault(group.Key) + group.Count() > int.MaxValue)
				result.Errors.Add(new(group.First().Number, "BookId", "CopyNumberLimit", "This title has reached its copy numbering limit."));

		await ValidateIdentityAsync(tenant, rows, result.Errors);
		await ValidateTitlesAsync(tenant, newRows, configuration, result.Errors);
		result.NewTitleCount = newRows.Where(row => !string.IsNullOrEmpty(row.TitleKey))
			.Select(row => row.TitleKey).Distinct(StringComparer.OrdinalIgnoreCase).Count();
		result.Rows = rows.Select(row => new LibraryStockImportPreviewRowDto(row.Number, row.SourceId, row.BookId, row.TitleKey,
			row.NewTitle?.Title ?? existingNames.GetValueOrDefault(row.BookId ?? Guid.Empty) ?? string.Empty, row.Accession,
			row.ReceivedOn, row.NewTitle?.Price, row.UnitCost, row.Currency)).ToList();
		if (result.Errors.Count == 0)
			result.Revision = Hash(new { Payload = payload, Configuration = configuration.Revision, Category = category,
				Position = new { position!.Value.Section, position.Value.Shelf, position.Value.Revision },
				Books = existing.OrderBy(row => row.Id), Stock = stock.OrderBy(row => row.BookId) });
		result.Errors = result.Errors.OrderBy(row => row.RowNumber).ThenBy(row => row.Field, StringComparer.Ordinal).ToList();
		return new(result, rows, existingNames, configuration.Revision);
	}

	internal static string Fingerprint(LibraryStockImportInput input) => Hash(new
	{
		input.Csv, input.AcademicYearId, input.SectionId, input.ShelfId, input.CategoryId, input.ReceivedOn, input.Source,
		SourceName = CleanSource(input.SourceName),
		ColumnMap = (input.ColumnMap ?? []).OrderBy(pair => pair.Key, StringComparer.Ordinal)
	});

	internal static string? CleanSource(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

	private async Task ValidateIdentityAsync(Guid tenant, List<LibraryImportRow> rows, List<LibraryStockImportErrorDto> errors)
	{
		var bySource = rows.Where(row => row.SourceId.Length is > 0 and <= 100)
			.ToLookup(row => LibraryStockImportRow.NormalizeSourceRowId(row.SourceId), StringComparer.Ordinal);
		var byAccession = rows.Where(row => row.Accession?.Length is > 0 and <= 50)
			.ToLookup(row => row.Accession!.ToUpperInvariant(), StringComparer.Ordinal);
		var keys = bySource.Select(group => group.Key).ToArray();
		var accessions = byAccession.Select(group => group.Key).ToArray();
		var imported = (await queries.ToListAsync((await accepted.GetQueryableAsync())
			.Where(row => row.TenantId == tenant && Enumerable.Contains(keys, row.NormalizedSourceRowId))
			.Select(row => row.NormalizedSourceRowId))).ToHashSet(StringComparer.Ordinal);
		var occupied = (await queries.ToListAsync((await copies.GetQueryableAsync())
			.Where(row => row.TenantId == tenant && Enumerable.Contains(accessions, row.NormalizedAccessionNumber))
			.Select(row => row.NormalizedAccessionNumber))).ToHashSet(StringComparer.Ordinal);
		foreach (var group in bySource)
			foreach (var row in group)
				if (imported.Contains(group.Key))
					errors.Add(new(row.Number, "SourceRowId", "AlreadyImported", "This source row already belongs to a committed school import."));
				else if (group.Skip(1).Any())
					errors.Add(new(row.Number, "SourceRowId", "DuplicateSourceRow", "Use a different stable source ID for each copy row."));
		foreach (var group in byAccession)
			foreach (var row in group)
				if (occupied.Contains(group.Key))
					errors.Add(new(row.Number, "Accession", "AccessionRegistered", "This accession is already registered at the school."));
				else if (group.Skip(1).Any())
					errors.Add(new(row.Number, "Accession", "DuplicateAccession", "Use a different accession for each copy row."));
	}

	private async Task ValidateTitlesAsync(Guid tenant, List<LibraryImportRow> rows, LibraryCatalogueConfigurationDto configuration,
		List<LibraryStockImportErrorDto> errors)
	{
		var groups = rows.GroupBy(row => row.TitleKey ?? string.Empty, StringComparer.OrdinalIgnoreCase).ToArray();
		var definitions = groups.Where(group => group.Key.Length > 0).Select(group => group.First().NewTitle!).ToArray();
		var isbns = definitions.Select(title => title.Isbn).Where(value => !string.IsNullOrEmpty(value)).Distinct().ToArray();
		var schoolIsbns = isbns.Length == 0 ? [] : await queries.ToListAsync((await books.GetQueryableAsync())
			.Where(row => row.TenantId == tenant && Enumerable.Contains(isbns, row.Isbn.Replace("-", "").Replace(" ", "").ToUpper()))
			.Select(row => new { Isbn = row.Isbn.Replace("-", "").Replace(" ", "").ToUpper(), Edition = (row.Edition ?? "").ToLower() }));
		var taken = schoolIsbns.Select(row => (row.Isbn, row.Edition)).ToHashSet();
		var sameFile = definitions.Where(title => !string.IsNullOrEmpty(title.Isbn))
			.GroupBy(title => (title.Isbn!, (title.Edition ?? "").ToLower())).Where(group => group.Skip(1).Any())
			.Select(group => group.Key).ToHashSet();
		foreach (var group in groups)
		{
			var first = group.First().NewTitle!;
			foreach (var row in group)
			{
				var title = row.NewTitle!;
				if (group.Key.Length > 0 && first != title)
					errors.Add(new(row.Number, "TitleKey", "TitleDetailsDiffer", "Use identical catalogue details on every row sharing this title key."));
				if (!string.IsNullOrEmpty(title.Isbn) && (taken.Contains((title.Isbn, (title.Edition ?? "").ToLower())) ||
					sameFile.Contains((title.Isbn, (title.Edition ?? "").ToLower()))))
					errors.Add(new(row.Number, "Isbn", "DuplicateEdition", "An ISBN and edition may identify one title. Use the existing BookId or one shared new TitleKey."));
				try { LibraryCatalogueRules.ValidateMetadata(configuration, title.Metadata); }
				catch (BusinessException exception)
				{
					errors.Add(new(row.Number, exception.Data["Field"] as string ?? "Title", exception.Code!, exception.Message));
				}
			}
		}
	}

	private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
}
