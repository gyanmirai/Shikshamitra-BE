using ShikshaMitra.Library.Catalogue;

namespace ShikshaMitra.Library.Books;

public partial class Book
{
	public void InitializeCatalogueMetadata(LibraryCatalogueDefinition definition, LibraryCatalogueMetadata metadata) =>
		ApplyCatalogueMetadata(LibraryCatalogueRules.ValidateMetadata(definition, metadata));

	public void ReviseCatalogueMetadata(LibraryCatalogueDefinition definition, LibraryCatalogueMetadata metadata) =>
		ApplyCatalogueMetadata(LibraryCatalogueRules.ValidateMetadata(definition, metadata,
			new(Author, Isbn, Publisher, Edition, Subject, ApplicableAge, Language, LanguageCode, MaterialCode)));

	private void ApplyCatalogueMetadata(LibraryCatalogueMetadata metadata)
	{
		Author = metadata.Author?.Trim() ?? string.Empty;
		Isbn = metadata.Isbn ?? string.Empty;
		Publisher = metadata.Publisher?.Trim() ?? string.Empty;
		Edition = metadata.Edition?.Trim() ?? string.Empty;
		Subject = metadata.Subject?.Trim() ?? string.Empty;
		ApplicableAge = metadata.ApplicableAge?.Trim() ?? string.Empty;
		Language = metadata.LegacyLanguage;
		LanguageCode = metadata.LanguageCode;
		MaterialCode = metadata.MaterialCode;
	}
}
