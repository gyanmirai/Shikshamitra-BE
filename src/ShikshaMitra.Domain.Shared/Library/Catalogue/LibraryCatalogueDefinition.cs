using System.Collections.Generic;

namespace ShikshaMitra.Library.Catalogue;

public class LibraryCatalogueDefinition
{
	public List<string> RequiredFields { get; set; } = [];
	public List<LibraryCatalogueChoice> Languages { get; set; } = [];
	public List<LibraryCatalogueChoice> Materials { get; set; } = [];
}

public class LibraryCatalogueChoice
{
	public string Code { get; set; } = string.Empty;
	public string Label { get; set; } = string.Empty;
	public bool IsActive { get; set; } = true;
}
