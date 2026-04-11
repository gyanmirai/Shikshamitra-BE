using System;

namespace ShikshaMitra.HR.Payroll.Settings;

public class UpdateEarningComponentDto
{
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public EarningComponentType ComponentType { get; set; }
	public CalculationType CalculationType { get; set; }
	public decimal? Amount { get; set; }
	public decimal? Percentage { get; set; }
	public Guid? SelectedEarningComponentId { get; set; }
	public ComponentDependency DependOn { get; set; }
	public bool VisibleInPayslip { get; set; } = true;
	public int DisplayOrder { get; set; } = 0;
}
