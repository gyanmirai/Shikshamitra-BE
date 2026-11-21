using System.Collections.Generic;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Library.Catalogue;

namespace ShikshaMitra.Library.Books;

public class LibraryBookEditorOptionsDto
{
	public List<NamedIdDto> BookCategories { get; set; } = [];
	public List<LibraryCopyMoveSectionDto> Sections { get; set; } = [];
	public LibraryCatalogueConfigurationDto? CatalogueConfiguration { get; set; }
}
