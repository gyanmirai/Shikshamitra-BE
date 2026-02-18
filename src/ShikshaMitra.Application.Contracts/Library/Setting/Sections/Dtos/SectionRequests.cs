using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Library.Setting.Sections.Dtos;

public class SectionCreateRequest
{
	[Required]
	public string? SectionName { get; set; }

	[Required]
	public int NumberOfShelf { get; set; }

	[Required]
	public List<string> ShelfItems { get; set; } = new();
}

public class SectionUpdateRequest
{
	[Required]
	public string? SectionName { get; set; }

	[Required]
	public int NumberOfShelf { get; set; }

	[Required]
	public List<string> ShelfItems { get; set; } = new();
}
