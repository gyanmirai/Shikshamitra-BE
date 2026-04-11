using System;
using System.Collections.Generic;

namespace ShikshaMitra.Application.Contracts.Finance.Setting.ExpenseCategory.Dtos;

public class ExpenseCategoryResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public List<string> Tags { get; set; } = new();
}

public class PaginatedExpenseCategoryResponse
{
    public long TotalCount { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public List<ExpenseCategoryResponse> Items { get; set; }
}
