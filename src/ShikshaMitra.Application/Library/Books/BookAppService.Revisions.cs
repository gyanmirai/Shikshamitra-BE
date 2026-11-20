using System.Threading.Tasks;

namespace ShikshaMitra.Library.Books;

public partial class BookAppService
{
	private async Task<LibraryBookUpdatedResponse> CatalogueUpdatedResponseAsync(Book book, string message) => new()
	{
		Id = book.Id.ToString(), Message = message,
		MetadataRevision = LibraryCatalogueRevisionReader.Metadata(book),
		CopiesRevision = await catalogueRevisions.CopiesAsync(book)
	};
}
