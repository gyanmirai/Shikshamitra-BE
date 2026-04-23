using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Accounts.Settings.TransactionCategories.Dtos;

public class TransactionCategoryResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public List<string> Tags { get; set; } = new();
}

public class PaginatedTransactionCategoryResponse
{
    public long TotalCount { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public List<TransactionCategoryResponse> Items { get; set; }
}
