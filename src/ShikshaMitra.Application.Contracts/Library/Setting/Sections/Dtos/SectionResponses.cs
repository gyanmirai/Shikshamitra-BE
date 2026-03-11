using System;
using System.Collections.Generic;

namespace ShikshaMitra.Library.Setting.Sections.Dtos;

public class SectionDto
{
	public Guid Id { get; set; }
	public string? SectionName { get; set; }
	public int NumberOfShelf { get; set; }

	public List<ShelfDetailDto> ShelfDetails { get; set; } = new();
}

public class ShelfDetailDto
{
	public string? ShelfName { get; set; }
	public Guid? Id { get; set; }
}

public class SectionDetailsResponse
{
	public string? Id { get; set; }
	public string? SectionName { get; set; }
	public int NumberOfShelf { get; set; }
	public List<ShelfDetailDto> ShelfDetails { get; set; } = new();
}

public class PaginatedSectionResponse
{
	public long TotalCount { get; set; }
	public int PageSize { get; set; }
	public int TotalPages { get; set; }
	public int CurrentPage { get; set; }
	public List<SectionDto> Items { get; set; } = new();
}
