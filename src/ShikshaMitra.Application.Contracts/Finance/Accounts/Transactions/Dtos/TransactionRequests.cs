using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Finance.Accounts.Transactions.Dtos;

public class TransactionCreateRequest
{
	public Guid? CashCollectionId { get; set; }
	public string? CashConcurrencyStamp { get; set; }
    public Guid? IdempotencyKey { get; set; }
    [StringLength(200)]
    public string? Counterparty { get; set; }
    public Guid? BankAccountId { get; set; }
    [StringLength(100)]
    public string? PaymentReference { get; set; }
	[Required]
	public Guid CategoryId { get; set; }

	[Required]
	public string Name { get; set; } = null!; // Request binding defers initialization; retain the null default and existing validation.

	public List<string> CategoryTags { get; set; } = [];

	[Required]
	[Range(typeof(decimal), "0.01", "999999999999.99", ErrorMessage = "Amount must be between 0.01 and 999999999999.99")]
	public decimal Amount { get; set; }

	[Required]
	[Range(0, 6, ErrorMessage = "PaymentMethod must be between 0 and 6")]
	public int PaymentMethod { get; set; }

	[Required]
	public DateTimeOffset PaymentDate { get; set; }

	public string? Notes { get; set; }
}

public class TransactionUpdateRequest
{
	[Required]
	public Guid CategoryId { get; set; }

	[Required]
	public string Name { get; set; } = null!; // Request binding defers initialization; retain the null default and existing validation.

	public List<string> CategoryTags { get; set; } = [];

	[Required]
	[Range(typeof(decimal), "0.01", "999999999999.99", ErrorMessage = "Amount must be between 0.01 and 999999999999.99")]
	public decimal Amount { get; set; }

	[Required]
	[Range(0, 6, ErrorMessage = "PaymentMethod must be between 0 and 6")]
	public int PaymentMethod { get; set; }

	[Required]
	public DateTime PaymentDate { get; set; }

	public string? Notes { get; set; }
}

public class GetTransactionsRequest
{
	public int Page { get; set; } = 1;
	public int PageSize { get; set; } = 20;
	public string? Search { get; set; }
}
