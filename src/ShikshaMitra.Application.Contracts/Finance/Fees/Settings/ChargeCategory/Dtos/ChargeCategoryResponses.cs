using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Fees.Settings.Dtos;

public class ChargeCategoryResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public bool IsApprovalRequired { get; set; }
    public string? Description { get; set; }
}

public class PaginatedChargeCategoryResponse
{
    public long TotalCount { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public List<ChargeCategoryResponse> Items { get; set; }
}
