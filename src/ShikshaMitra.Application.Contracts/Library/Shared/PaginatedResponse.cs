using System.Collections.Generic;

namespace ShikshaMitra.Library.Shared;

public class PaginatedResponse<T>
{
	public int TotalPages { get; set; }
	public int PageSize { get; set; }
	public long TotalCount { get; set; }
	public int CurrentPage { get; set; }
	public List<T> Items { get; set; } = new();
}
