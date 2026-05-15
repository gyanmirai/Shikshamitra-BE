using System;

namespace ShikshaMitra.HR.Payroll.Settings;

public class DeductionComponentUpdateRequestDto
{
    public string DeductionComponentName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string DeductionType { get; set; } = string.Empty;
    public string CalculationType { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public decimal? Percentage { get; set; }
    public Guid? SelectedDeductionComponentId { get; set; }
    public bool VisibleInPayslip { get; set; } = true;
    public string? ApplyMode { get; set; }
    public string? EffectiveMonth { get; set; }
    public string? PreviewHash { get; set; }
}
