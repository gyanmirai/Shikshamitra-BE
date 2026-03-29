using System;

namespace ShikshaMitra.HR.Payroll.Settings;

public class UpdateDeductionComponentDto
{
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public DeductionComponentType ComponentType { get; set; }
	public CalculationType CalculationType { get; set; }
	public decimal? Amount { get; set; }
	public decimal? Percentage { get; set; }
	public Guid? SelectedDeductionComponentId { get; set; }
	public ComponentDependency DependOn { get; set; }
	public bool VisibleInPayslip { get; set; } = true;
	public int DisplayOrder { get; set; } = 0;
}
