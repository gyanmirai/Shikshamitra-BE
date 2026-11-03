using System.Collections.Generic;

namespace ShikshaMitra.Finance.Fees.Settings.Dtos;

public class FeeHeadListResponse
{
    public long TotalCount { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public List<FeeHeadResponse> Items { get; set; } = null!; // The response mapper populates these members after construction.
}
