using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Finance.Accounts.Settings.TransactionCategories.Dtos;

public class CreateTransactionCategoryRequest
{
	[Required]
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public List<string> Tags { get; set; } = [];
	[Required]
	public Guid? DefaultAccountHeadId { get; set; }
	public string? ConcurrencyStamp { get; set; }
}

public class UpdateTransactionCategoryRequest : CreateTransactionCategoryRequest;
