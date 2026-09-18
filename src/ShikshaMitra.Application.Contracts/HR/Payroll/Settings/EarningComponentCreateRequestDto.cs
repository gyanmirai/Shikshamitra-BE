using System;

namespace ShikshaMitra.HR.Payroll.Settings;

public class EarningComponentCreateRequestDto
{
    public string EarningComponentName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string EarningType { get; set; } = string.Empty;
    public string CalculationType { get; set; } = string.Empty;
    public string CalculationBase { get; set; } = "COMPONENT";
    public decimal? Amount { get; set; }
    public decimal? Percentage { get; set; }
    public Guid? SelectedEarningComponentId { get; set; }
    public bool VisibleInPayslip { get; set; } = true;
}
