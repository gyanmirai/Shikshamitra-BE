using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.Payroll.Settings;

/// <summary>
/// DTO matching the OpenAPI PayrollComponentCreateRequest schema for creating/updating earning or deduction components.
/// </summary>
public class PayrollComponentCreateRequestDto
{
    [Required]
    public string ComponentName { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    public bool IsFixed { get; set; }

    public string? ComponentType { get; set; } // "FIXED" or "PERCENTAGE"

    public double? Amount { get; set; }

    public bool VisibleInPayslip { get; set; }

    public string? DependOn { get; set; } // "GROSS", "BASIC", "CUSTOM_COMPONENT"

    public Guid? CustomComponentId { get; set; }
}
