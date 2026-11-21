using System.Linq;
using System.Text.Json;
using ShikshaMitra.Library.Books;
using Shouldly;

namespace ShikshaMitra.Library;

public partial class LibraryCatalogueReadAuthority_Tests
{
	private static string? OpenedRevision(object value, string property)
	{
		var json = JsonSerializer.SerializeToElement(value);
		return json.TryGetProperty(property, out var field) ? field.GetString() : null;
	}

	private static string RequiredRevision(object value, string property)
	{
		var revision = OpenedRevision(value, property);
		revision.ShouldNotBeNullOrWhiteSpace();
		return revision;
	}

	private static UpdateBookRequest MetadataEdit(BookDetailDto opened, string title, bool missing = false) =>
		JsonSerializer.Deserialize<UpdateBookRequest>(JsonSerializer.Serialize(new
		{
			Title = title, opened.Author, opened.Isbn, opened.Publisher, opened.Edition, opened.Subject,
			CategoryId = opened.Category.Id, opened.Language, opened.LanguageCode, opened.MaterialCode, opened.ApplicableAge,
			opened.Pages, opened.Price, opened.PriceText, opened.CoverImage, NumberOfCopies = opened.Copies.Count,
			ExpectedMetadataRevision = missing ? null : OpenedRevision(opened, "MetadataRevision")
		}))!;

	private static PutBookCopiesRequest CopiesEdit(BookDetailDto opened, bool missing = false) =>
		JsonSerializer.Deserialize<PutBookCopiesRequest>(JsonSerializer.Serialize(new
		{
			ExpectedCopiesRevision = missing ? null : OpenedRevision(opened, "CopiesRevision"),
			Copies = opened.Copies.Select(copy => new BookCopyItem
			{
				CopyId = copy.CopyId, CopyNumber = copy.CopyNumber, AccessionNumber = copy.AccessionNo,
				SectionId = copy.Section.Id, ShelfId = copy.Shelf.Id
			}).ToList()
		}))!;
}
