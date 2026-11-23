using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Fees.Settings.Dtos;

public class ChargeCategoryResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!; // The response mapper populates these members after construction.
    public bool IsApprovalRequired { get; set; }
    public string? Description { get; set; }
	public Guid? IncomeCategoryId { get; set; }
	public Guid? IncomeAccountHeadId { get; set; }
	public string? IncomeCategoryName { get; set; }
	public string? IncomeAccountHeadName { get; set; }
	public bool IsAccountingReady { get; set; }
	public bool IsActive { get; set; }
	public string ConcurrencyStamp { get; set; } = string.Empty;
}

public class PaginatedChargeCategoryResponse
{
    public long TotalCount { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public List<ChargeCategoryResponse> Items { get; set; } = null!; // The response mapper populates these members after construction.
}

public class ChargeCategoryUsageResponse
{
	public Guid CategoryId { get; set; }
	public string CategoryName { get; set; } = string.Empty;
	/// <summary>Distinct charge requests, including requests without assignment lines.</summary>
	public int ChargeCount { get; set; }
	public int StudentAssignmentCount { get; set; }
	/// <summary>Up to ten charge names within the caller's permitted student scope.</summary>
	public List<string> ChargeNames { get; set; } = [];
	public bool CanDelete => ChargeCount == 0;
}
