using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Accounts.Incomes.Dtos;

// ────────────────────────────────────────────────────────────────
// List response  →  GET /api/incomes
// ────────────────────────────────────────────────────────────────
public class IncomeListResponse
{
	public int TotalCount { get; set; }
	public int CurrentPage { get; set; }
	public int PageSize { get; set; }
	public int TotalPages { get; set; }
	public List<IncomeListItem> Items { get; set; } = new();
}

public class IncomeListItem
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
public class IncomeCreateUpdateResponse
{
	public bool Success { get; set; } = true;
	public IncomeResponseData Data { get; set; }
}

public class IncomeResponseData
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
// Detail response  →  GET /api/incomes/{income-id}
// ────────────────────────────────────────────────────────────────
public class IncomeDetailsResponse
{
	public IncomeInformation IncomeInformation { get; set; }
	public List<IncomeTransaction> IncomeList { get; set; } = new();
}

public class IncomeInformation
{
	public Guid IncomeCategoryId { get; set; }
	public string IncomeCategoryName { get; set; }
	public List<string> CategoryTags { get; set; } = new();
	public double TotalAmountReceived { get; set; }
}

public class IncomeTransaction
{
	public Guid TransactionId { get; set; }
	public string IncomeName { get; set; }
	public DateTime ReceivedDate { get; set; }
	public int PaymentMethod { get; set; }
	public double Amount { get; set; }
}

// ────────────────────────────────────────────────────────────────
// Delete response  →  DELETE 200
// ────────────────────────────────────────────────────────────────
public class IncomeDeleteResponse
{
	public bool Success { get; set; } = true;
	public string Message { get; set; }
}
