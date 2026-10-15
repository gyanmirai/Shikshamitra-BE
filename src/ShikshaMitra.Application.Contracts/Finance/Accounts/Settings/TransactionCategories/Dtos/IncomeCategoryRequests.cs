using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Finance.Accounts.Settings.TransactionCategories.Dtos;

public class CreateTransactionCategoryRequest
{
	[Required]
	[StringLength(AccountSettingConsts.MaxNameLength)]
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public List<string> Tags { get; set; } = [];
	[System.Text.Json.Serialization.JsonIgnore]
	public Guid? DefaultAccountHeadId { get; set; }
	public string? ConcurrencyStamp { get; set; }
}

public class UpdateTransactionCategoryRequest : CreateTransactionCategoryRequest;

// Preserve the JSON shape while allowing the Income route's stricter name limit.
public class CreateIncomeCategoryRequest : CreateTransactionCategoryRequest;
public class UpdateIncomeCategoryRequest : UpdateTransactionCategoryRequest;
