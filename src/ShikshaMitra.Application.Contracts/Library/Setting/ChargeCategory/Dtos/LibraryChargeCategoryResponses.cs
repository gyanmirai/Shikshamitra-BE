using System;
using System.Collections.Generic;

namespace ShikshaMitra.Library.Setting.ChargeCategory.Dtos;

public class LibraryChargeCategoryDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = null!; // The response mapper populates these members after construction.
	public decimal Amount { get; set; }
	public bool IsFixed { get; set; }
	public string Description { get; set; } = null!;
}

public class PaginatedLibraryChargeCategoryResponse
{
	public long TotalCount { get; set; }
	public List<LibraryChargeCategoryDto> Items { get; set; } = new();
	public int CurrentPage { get; set; }
	public int PageSize { get; set; }
	public int TotalPages { get; set; }
}
