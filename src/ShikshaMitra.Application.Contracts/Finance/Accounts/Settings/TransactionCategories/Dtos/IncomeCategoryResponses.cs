using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Accounts.Settings.TransactionCategories.Dtos;

public class TransactionCategoryResponse
{
	public Guid Id { get; set; }
	public Guid AcademicYearId { get; set; }
	public string? Code { get; set; }
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public List<string> Tags { get; set; } = [];
	public Guid? DefaultAccountHeadId { get; set; }
	public string? DefaultAccountHeadName { get; set; }
	public DateTimeOffset? EffectiveFrom { get; set; }
	public DateTimeOffset? EffectiveTo { get; set; }
	public bool IsActive { get; set; }
	public bool IsSystemCategory { get; set; }
	public string? DeactivationReason { get; set; }
	public int Version { get; set; }
	public string ConcurrencyStamp { get; set; } = string.Empty;
	public bool IsUsedInDownstream { get; set; }
	public string? DeletionBlockedReason { get; set; }
	public bool CanDelete { get; set; }
}

public class PaginatedTransactionCategoryResponse
{
	public long TotalCount { get; set; }
	public int CurrentPage { get; set; }
	public int PageSize { get; set; }
	public int TotalPages { get; set; }
	public List<TransactionCategoryResponse> Items { get; set; } = [];
}
