using ShikshaMitra.Library.Books;

namespace ShikshaMitra.Library.Catalogue;

public record LibraryCatalogueMetadata(
	string? Author, string? Isbn, string? Publisher, string? Edition, string? Subject,
	string? ApplicableAge, Language? LegacyLanguage, string? LanguageCode, string? MaterialCode);
