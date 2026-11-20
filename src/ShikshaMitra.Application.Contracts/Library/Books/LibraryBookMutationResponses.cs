using ShikshaMitra.Library.Shared;

namespace ShikshaMitra.Library.Books;

public class LibraryBookCreatedResponse : GenericCreatedResponse
{
	public string MetadataRevision { get; set; } = null!;
	public string CopiesRevision { get; set; } = null!;
}

public class LibraryBookUpdatedResponse : GenericUpdatedResponse
{
	public string MetadataRevision { get; set; } = null!;
	public string CopiesRevision { get; set; } = null!;
}
