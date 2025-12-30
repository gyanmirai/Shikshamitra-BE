using System;
using System.Collections.Generic;

namespace ShikshaMitra.HR.Payroll.Settings;

/// <summary>
/// Paginated response for deduction components as per OpenAPI contract.
/// </summary>
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
    public string ComponentName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsFixed { get; set; }
    public string? ComponentType { get; set; }
    public double Amount { get; set; }
    public bool VisibleInPayslip { get; set; }
    public CustomComponentDto? CustomComponent { get; set; }
}

public class CustomComponentDto
{
    public Guid Id { get; set; }
    public string ComponentName { get; set; } = string.Empty;
    public string DependOn { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsFixed { get; set; }
    public bool VisibleInPayslip { get; set; }
}
