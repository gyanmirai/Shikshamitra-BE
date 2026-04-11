using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.Payroll.Settings;

public class EarningComponentDto : FullAuditedEntityDto<Guid>
{
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public EarningComponentType ComponentType { get; set; }
	public CalculationType CalculationType { get; set; }
	public decimal? Amount { get; set; }
	public decimal? Percentage { get; set; }
	public Guid? SelectedEarningComponentId { get; set; }
	public string? SelectedEarningComponentName { get; set; }
	public string? SelectedEarningComponentDescription { get; set; }
	public bool? SelectedEarningComponentVisibleInPayslip { get; set; }
	public ComponentDependency DependOn { get; set; }
	public bool VisibleInPayslip { get; set; }
	public int DisplayOrder { get; set; }
	public bool IsActive { get; set; }
}
