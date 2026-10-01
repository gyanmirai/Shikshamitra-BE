using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Accounts.Transactions.Dtos;

// ────────────────────────────────────────────────────────────────
// List response  →  GET /api/transactions
// ────────────────────────────────────────────────────────────────
public class TransactionListResponse
{
	public int TotalCount { get; set; }
	public int CurrentPage { get; set; }
	public int PageSize { get; set; }
	public int TotalPages { get; set; }
	public List<TransactionListItem> Items { get; set; } = new();
}

public class TransactionListItem
{
	public Guid Id { get; set; }
	public Guid CategoryId { get; set; }
	public string CategoryName { get; set; }
	public List<string> CategoryTags { get; set; } = new();
	public string Name { get; set; }
	public decimal Amount { get; set; }
	public int PaymentMethod { get; set; }
	public DateTime PaymentDate { get; set; }
	public string? Notes { get; set; }
	public DateTime CreatedAt { get; set; }
}

// ────────────────────────────────────────────────────────────────
// Create / Update response  →  POST 201 / PUT 200
// ────────────────────────────────────────────────────────────────
public class TransactionCreateUpdateResponse
{
	public bool Success { get; set; } = true;
	public TransactionResponseData Data { get; set; }
}

public class TransactionResponseData
{
	public Guid Id { get; set; }
	public Guid CategoryId { get; set; }
	public string CategoryName { get; set; }
	public List<string> CategoryTags { get; set; } = new();
	public string Name { get; set; }
	public decimal Amount { get; set; }
	public int PaymentMethod { get; set; }
	public DateTime PaymentDate { get; set; }
	public string? Notes { get; set; }
	public DateTime CreatedAt { get; set; }
}

// ────────────────────────────────────────────────────────────────
// Detail response  →  GET /api/transactions/{transaction-id}
// ────────────────────────────────────────────────────────────────
public class TransactionDetailsResponse
{
	public TransactionInformation TransactionInformation { get; set; }
	public List<TransactionItem> TransactionList { get; set; } = new();
}

public class TransactionInformation
{
	public Guid TransactionCategoryId { get; set; }
	public string TransactionCategoryName { get; set; }
	public List<string> CategoryTags { get; set; } = new();
	public decimal TotalAmountReceived { get; set; }
}

public class TransactionItem
{
	public Guid TransactionId { get; set; }
	public string IncomeName { get; set; }
	public DateTime ReceivedDate { get; set; }
	public int PaymentMethod { get; set; }
	public decimal Amount { get; set; }
}

// ────────────────────────────────────────────────────────────────
// Delete response  →  DELETE 200
// ────────────────────────────────────────────────────────────────
public class TransactionDeleteResponse
{
	public bool Success { get; set; } = true;
	public string Message { get; set; }
}
