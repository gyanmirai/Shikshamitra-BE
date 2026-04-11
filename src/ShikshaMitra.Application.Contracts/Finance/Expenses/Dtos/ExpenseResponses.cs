using System;
using System.Collections.Generic;

namespace ShikshaMitra.Application.Contracts.Finance.Expenses.Dtos;

// ────────────────────────────────────────────────────────────────
// List response  →  GET /api/expenses
// ────────────────────────────────────────────────────────────────
public class ExpenseListResponse
{
	public int TotalCount { get; set; }
	public int CurrentPage { get; set; }
	public int PageSize { get; set; }
	public int TotalPages { get; set; }
	public List<ExpenseListItem> Items { get; set; } = new();
}

public class ExpenseListItem
{
	public Guid Id { get; set; }
	public Guid CategoryId { get; set; }
	public string CategoryName { get; set; }
	public List<string> CategoryTags { get; set; } = new();
	public string Name { get; set; }
	public double Amount { get; set; }
	public int PaymentMethod { get; set; }
	public DateTime PaymentDate { get; set; }
	public string? Notes { get; set; }
	public DateTime CreatedAt { get; set; }
}

// ────────────────────────────────────────────────────────────────
// Create / Update response  →  POST 201 / PUT 200
// ────────────────────────────────────────────────────────────────
public class ExpenseCreateUpdateResponse
{
	public bool Success { get; set; } = true;
	public ExpenseResponseData Data { get; set; }
}

public class ExpenseResponseData
{
	public Guid Id { get; set; }
	public Guid CategoryId { get; set; }
	public string CategoryName { get; set; }
	public List<string> CategoryTags { get; set; } = new();
	public string Name { get; set; }
	public double Amount { get; set; }
	public int PaymentMethod { get; set; }
	public DateTime PaymentDate { get; set; }
	public string? Notes { get; set; }
	public DateTime CreatedAt { get; set; }
}

// ────────────────────────────────────────────────────────────────
// Detail response  →  GET /api/expenses/{expense-id}
// ────────────────────────────────────────────────────────────────
public class ExpenseDetailsResponse
{
	public ExpenseInformation ExpenseInformation { get; set; }
	public List<ExpenseTransaction> ExpenseList { get; set; } = new();
}

public class ExpenseInformation
{
	public Guid ExpenseCategoryId { get; set; }
	public string ExpenseCategoryName { get; set; }
	public List<string> CategoryTags { get; set; } = new();
	public double TotalAmountSpent { get; set; }
}

public class ExpenseTransaction
{
	public Guid TransactionId { get; set; }
	public string ExpenseName { get; set; }
	public DateTime SpentDate { get; set; }
	public int PaymentMethod { get; set; }
	public double Amount { get; set; }
}

// ────────────────────────────────────────────────────────────────
// Delete response  →  DELETE 200
// ────────────────────────────────────────────────────────────────
public class ExpenseDeleteResponse
{
	public bool Success { get; set; } = true;
	public string Message { get; set; }
}
