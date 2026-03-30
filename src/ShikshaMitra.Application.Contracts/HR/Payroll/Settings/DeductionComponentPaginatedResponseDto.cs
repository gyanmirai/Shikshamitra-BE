using System;
using System.Collections.Generic;

namespace ShikshaMitra.HR.Payroll.Settings;

public class DeductionComponentPaginatedResponseDto
{
	public int TotalCount { get; set; }
	public int CurrentPage { get; set; }
	public int PageSize { get; set; }
	public int TotalPages { get; set; }
	public List<DeductionComponentResponseDto> Items { get; set; } = new();
}

public class DeductionComponentResponseDto
{
	public Guid Id { get; set; }
	public string DeductionComponentName { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string DeductionType { get; set; } = string.Empty;
	public string CalculationType { get; set; } = string.Empty;
	public decimal? Amount { get; set; }
	public decimal? Percentage { get; set; }
	public Guid? SelectedDeductionComponentId { get; set; }
	public bool VisibleInPayslip { get; set; }
	public CustomComponentDto? CustomComponent { get; set; }
}

public class CustomComponentDto
{
	public Guid Id { get; set; }
	public string ComponentName { get; set; } = string.Empty;
	public string ComponentOf { get; set; } = string.Empty;
	public string? Description { get; set; }
	public bool VisibleInPayslip { get; set; }
}
