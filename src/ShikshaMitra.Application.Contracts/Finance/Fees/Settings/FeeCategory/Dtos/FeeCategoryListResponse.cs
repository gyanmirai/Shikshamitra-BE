using System.Collections.Generic;

namespace ShikshaMitra.Finance.Fees.Settings.Dtos;

public class FeeCategoryListResponse
{
    public long TotalCount { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public List<FeeCategoryResponse> Items { get; set; } = null!; // The response mapper populates these members after construction.
}
