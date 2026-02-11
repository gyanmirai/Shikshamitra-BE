using System;
using System.Collections.Generic;

namespace ShikshaMitra.Application.Contracts.Finance.Setting.IncomeCategory.Dtos;

public class IncomeCategoryResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public List<string> Tags { get; set; } = new();
}

public class PaginatedIncomeCategoryResponse
{
    public long TotalCount { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public List<IncomeCategoryResponse> Items { get; set; }
}
