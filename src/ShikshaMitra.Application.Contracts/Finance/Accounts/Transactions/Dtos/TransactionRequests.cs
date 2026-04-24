using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Finance.Accounts.Transactions.Dtos;

public class TransactionCreateRequest
{
	[Required]
	public Guid CategoryId { get; set; }

	[Required]
	public string Name { get; set; }

	[Required]
	public List<string> CategoryTags { get; set; }

	[Required]
	[Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
	public double Amount { get; set; }

	[Required]
	[Range(0, 6, ErrorMessage = "PaymentMethod must be between 0 and 6")]
	public int PaymentMethod { get; set; }

	[Required]
	public DateTime PaymentDate { get; set; }

	public string? Notes { get; set; }
}

public class TransactionUpdateRequest
{
	[Required]
	public Guid CategoryId { get; set; }

	[Required]
	public string Name { get; set; }

	[Required]
	public List<string> CategoryTags { get; set; }

	[Required]
	[Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
	public double Amount { get; set; }

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
