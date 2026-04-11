using System.Collections.Generic;

namespace ShikshaMitra.Application.Contracts.Finance.Setting.FeeCategory.Dtos;

public class FeeCategoryListResponse
{
    public long TotalCount { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public List<FeeCategoryResponse> Items { get; set; }
}
