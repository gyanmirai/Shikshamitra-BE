using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualBasic.FileIO;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Catalogue;
using Volo.Abp;

namespace ShikshaMitra.Library.Receiving;

internal sealed record LibraryImportTitle(string Title, string? Author, string? Isbn, string? Edition, string? Publisher,
	string? Subject, string? LanguageCode, string? MaterialCode, string? ApplicableAge, string? Price, int Pages)
{
	public LibraryCatalogueMetadata Metadata => new(Author, Isbn, Publisher, Edition, Subject, ApplicableAge, null, LanguageCode, MaterialCode);

	public CreateBookRequest CreateRequest(Guid categoryId, string revision) => new()
	{
		Title = Title, Author = Author, Isbn = Isbn, Edition = Edition ?? string.Empty, Publisher = Publisher ?? string.Empty,
		Subject = Subject ?? string.Empty, LanguageCode = LanguageCode, MaterialCode = MaterialCode,
		ApplicableAge = ApplicableAge ?? string.Empty, PriceText = Price, Pages = Pages, CategoryId = categoryId,
		ExpectedCatalogueConfigurationRevision = revision
	};
}

internal sealed record LibraryImportRow(int Number, string SourceId, Guid? BookId, string? TitleKey,
	LibraryImportTitle? NewTitle, string? Accession, DateOnly ReceivedOn, string? UnitCost, string? Currency);

internal static class LibraryStockImportCsv
{
	internal const int MaximumBytes = 1024 * 1024;
	private const int MaximumColumns = 64;
	private static readonly string[] MetadataColumns = ["Title", "Author", "Isbn", "Edition", "Publisher", "Subject",
		"LanguageCode", "MaterialCode", "ApplicableAge", "CataloguePrice", "Pages"];
	private static readonly string[] Columns = ["SourceRowId", "BookId", "TitleKey", .. MetadataColumns,
		"Accession", "ReceivedOn", "UnitCost", "Currency"];

	internal static List<LibraryImportRow> Parse(LibraryStockImportInput input, List<LibraryStockImportErrorDto> errors)
	{
		var rows = new List<LibraryImportRow>();
		if (string.IsNullOrWhiteSpace(input.Csv) || Encoding.UTF8.GetByteCount(input.Csv) > MaximumBytes || input.Csv.Contains('\0'))
		{
			errors.Add(new(null, "Csv", "InvalidFile", "Choose a UTF-8 CSV file up to 1 MiB."));
			return rows;
		}
		using var reader = new TextFieldParser(new StringReader(input.Csv.TrimStart('\uFEFF')));
		reader.SetDelimiters(",");
		reader.HasFieldsEnclosedInQuotes = true;
		reader.TrimWhiteSpace = false;
		try
		{
			var headings = reader.ReadFields()?.Select(heading => heading.Trim()).ToArray() ?? [];
			var indices = ResolveColumns(input, headings, errors);
			if (indices == null) return rows;
			var dataLines = 0;
			while (!reader.EndOfData)
			{
				if (++dataLines > LibraryStockImportBatch.MaximumRows)
				{
					errors.Add(new(null, "Csv", "RowLimit", "A batch supports at most 1,000 copy rows."));
					break;
				}
				var values = reader.ReadFields() ?? [];
				var number = FirstPhysicalLine(reader.LineNumber, input.Csv, values);
				if (values.Length != headings.Length)
				{
					errors.Add(new(number, "Csv", "ColumnCount", "This row must have the same number of fields as the header."));
					continue;
				}
				rows.Add(ReadRow(number, values, indices, input.ReceivedOn, errors));
			}
			if (dataLines == 0) errors.Add(new(null, "Csv", "NoRows", "Include at least one copy row."));
		}
		catch (MalformedLineException exception)
		{
			errors.Add(new((int)exception.LineNumber, "Csv", "BadCsv", "Check quoting and delimiters at this line."));
		}
		return rows;
	}

	private static int FirstPhysicalLine(long nextLine, string file, string[] fields)
	{
		if (nextLine < 0) nextLine = CountLineBreaks(file) + (file.EndsWith('\r') || file.EndsWith('\n') ? 1 : 2);
		return (int)nextLine - 1 - fields.Sum(CountLineBreaks);
	}

	private static int CountLineBreaks(string text)
	{
		var count = 0;
		for (var index = 0; index < text.Length; index++)
			if (text[index] == '\n' || text[index] == '\r' && (index + 1 == text.Length || text[index + 1] != '\n')) count++;
		return count;
	}

	private static Dictionary<string, int>? ResolveColumns(LibraryStockImportInput input, string[] headings,
		List<LibraryStockImportErrorDto> errors)
	{
		if (headings.Length is < 1 or > MaximumColumns || headings.Any(string.IsNullOrWhiteSpace) ||
			headings.Distinct(StringComparer.OrdinalIgnoreCase).Count() != headings.Length)
		{
			errors.Add(new(1, "Csv", "InvalidHeader", "Use 1 to 64 columns with unique, nonempty headings."));
			return null;
		}
		var map = input.ColumnMap ?? [];
		if (map.Any(pair => !Columns.Contains(pair.Key, StringComparer.Ordinal) || string.IsNullOrWhiteSpace(pair.Value)))
		{
			errors.Add(new(1, "ColumnMap", "InvalidColumnMap", "Map a supported import field to a CSV heading."));
			return null;
		}
		var indices = new Dictionary<string, int>(StringComparer.Ordinal);
		foreach (var column in Columns)
		{
			var mapped = map.TryGetValue(column, out var heading);
			var index = Array.FindIndex(headings, value => string.Equals(value, mapped ? heading!.Trim() : column, StringComparison.OrdinalIgnoreCase));
			if (index >= 0) indices.Add(column, index);
			else if (mapped || column == "SourceRowId")
				errors.Add(new(1, column, "MissingColumn", "The configured heading is missing from the CSV."));
		}
		if (indices.Values.Distinct().Count() != indices.Count)
			errors.Add(new(1, "ColumnMap", "DuplicateMapping", "Use a separate CSV heading for each import field."));
		return errors.Count == 0 ? indices : null;
	}

	private static LibraryImportRow ReadRow(int number, string[] values, Dictionary<string, int> indices,
		DateOnly defaultDate, List<LibraryStockImportErrorDto> errors)
	{
		string? Cell(string field) => indices.TryGetValue(field, out var index) && !string.IsNullOrWhiteSpace(values[index])
			? values[index].Trim() : null;
		void Error(string field, string code, string message) => errors.Add(new(number, field, code, message));
		var source = Cell("SourceRowId");
		if (source == null || source.Length > 100 || source.Any(char.IsControl))
			Error("SourceRowId", "InvalidSourceRow", "Enter a stable school-wide source row ID of at most 100 characters.");
		var accession = Cell("Accession");
		if (accession?.Length > 50 || accession?.Any(char.IsControl) == true)
			Error("Accession", "InvalidAccession", "Use at most 50 characters without control characters for the accession.");
		var day = defaultDate;
		if (Cell("ReceivedOn") is { } date &&
			(!DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day) || day == default))
			Error("ReceivedOn", "InvalidDate", "Enter a valid receipt date as YYYY-MM-DD or use the batch date.");
		var currency = Cell("Currency");
		if (!LibraryReceivingInputRules.TryCost(Cell("UnitCost"), currency, out var cost))
			Error("UnitCost", "InvalidCost", "Enter a nonnegative exact cost with a three-letter uppercase currency, or leave both blank.");

		var key = Cell("TitleKey");
		LibraryImportTitle? title = null;
		Guid? bookId = null;
		if (Cell("BookId") is { } book)
		{
			if (Guid.TryParseExact(book, "D", out var id) && id != Guid.Empty) bookId = id;
			else Error("BookId", "InvalidBookId", "Use the catalogue title ID in UUID format.");
			if (key != null || MetadataColumns.Any(field => Cell(field) != null))
				Error("BookId", "UnexpectedTitleMetadata", "Leave new-title fields blank when receiving an existing title.");
		}
		else
		{
			if (key == null || key.Length > 100 || key.Any(char.IsControl))
				Error("TitleKey", "InvalidTitleKey", "Use one file-specific title key of at most 100 characters for the new title.");
			var name = Cell("Title");
			if (name == null || name.Length > 256)
				Error("Title", "InvalidTitle", "Enter a title of at most 256 characters.");
			var pages = 0;
			if (Cell("Pages") is { } rawPages &&
				!int.TryParse(rawPages, NumberStyles.None, CultureInfo.InvariantCulture, out pages))
				Error("Pages", "InvalidPages", "Enter a nonnegative whole number of pages.");
			var rawPrice = Cell("CataloguePrice");
			string? price = null;
			try { price = LibraryCataloguePriceRules.Format(LibraryCataloguePriceRules.Resolve(null, rawPrice)); }
			catch (BusinessException exception) { Error("CataloguePrice", exception.Code!, exception.Message); }
			title = new(name ?? string.Empty, Cell("Author"), BookAppService.NormalizeIsbn(Cell("Isbn")), Cell("Edition"),
				Cell("Publisher"), Cell("Subject"), Cell("LanguageCode"), Cell("MaterialCode"), Cell("ApplicableAge"), price, pages);
		}
		return new(number, source ?? string.Empty, bookId, key, title, accession, day, LibraryReceivingInputRules.CostText(cost), currency);
	}
}
