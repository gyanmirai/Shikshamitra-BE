using System.Collections.Generic;

namespace ShikshaMitra.HR.Payroll.Settings;

/// <summary>
/// Paginated response DTO for PF rules list.
/// Matches the OpenAPI PaginatedPfRuleResponse schema.
/// </summary>
public class PaginatedPfRuleResponseDto
{
	/// <summary>
	/// Total number of PF rules
	/// </summary>
	public int TotalCount { get; set; }
	
	/// <summary>
	/// Current page number (1-based)
	/// </summary>
	public int CurrentPage { get; set; }
	
	/// <summary>
	/// Number of items per page
	/// </summary>
	public int PageSize { get; set; }
	
	/// <summary>
	/// Total number of pages
	/// </summary>
	public int TotalPages { get; set; }
	
	/// <summary>
	/// List of PF rules for the current page
	/// </summary>
	public List<PfRuleResponseDto> Items { get; set; } = new();
}

