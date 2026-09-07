using System.Collections.Generic;

namespace ShikshaMitra.Finance.Fees.Settings.Dtos;

public class FeeCategoryListResponse
{
    public long TotalCount { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public List<FeeCategoryResponse> Items { get; set; }
	public FeeSetupSummaryResponse Summary { get; set; } = new();
}

public class FeeSetupSummaryResponse
{
	public int CategoryCount { get; set; }
	public int FeeHeadCount { get; set; }
	public int ActiveRecordCount { get; set; }
	public int InactiveRecordCount { get; set; }
	public int InUseRecordCount { get; set; }
}
