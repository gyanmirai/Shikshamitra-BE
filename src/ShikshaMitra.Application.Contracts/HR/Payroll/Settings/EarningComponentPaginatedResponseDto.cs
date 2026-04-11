using System;
using System.Collections.Generic;

namespace ShikshaMitra.HR.Payroll.Settings;

public class EarningComponentPaginatedResponseDto
{
	public int TotalCount { get; set; }
	public int CurrentPage { get; set; }
	public int PageSize { get; set; }
	public int TotalPages { get; set; }
	public List<EarningComponentResponseDto> Items { get; set; } = new();
}

public class EarningComponentResponseDto
{
	public Guid Id { get; set; }
	public string EarningComponentName { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string EarningType { get; set; } = string.Empty;
	public string CalculationType { get; set; } = string.Empty;
	public decimal? Amount { get; set; }
	public decimal? Percentage { get; set; }
	public Guid? SelectedEarningComponentId { get; set; }
	public bool VisibleInPayslip { get; set; }
	public CustomComponentDto? CustomComponent { get; set; }
}
