using System;

namespace ShikshaMitra.HR.Payroll.Settings;

public class EarningComponentUpdateRequestDto
{
    public string EarningComponentName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string EarningType { get; set; } = string.Empty;
    public string CalculationType { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public decimal? Percentage { get; set; }
    public Guid? SelectedEarningComponentId { get; set; }
    public bool VisibleInPayslip { get; set; } = true;
}
