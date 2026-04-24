using System.Collections.Generic;

namespace ShikshaMitra.Finance.Fees.Settings.Dtos;

public class ReceiptBookListResponse
{
    public long TotalCount { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public List<ReceiptBookResponse> Items { get; set; }
}
