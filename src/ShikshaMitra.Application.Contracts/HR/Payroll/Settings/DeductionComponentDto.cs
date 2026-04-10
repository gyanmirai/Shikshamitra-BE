using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.Payroll.Settings;

public class DeductionComponentDto : FullAuditedEntityDto<Guid>
{
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public DeductionComponentType ComponentType { get; set; }
	public CalculationType CalculationType { get; set; }
	public decimal? Amount { get; set; }
	public decimal? Percentage { get; set; }
	public Guid? SelectedDeductionComponentId { get; set; }
	public string? SelectedDeductionComponentName { get; set; }
	public string? SelectedDeductionComponentDescription { get; set; }
	public bool? SelectedDeductionComponentVisibleInPayslip { get; set; }
	public ComponentDependency DependOn { get; set; }
	public bool VisibleInPayslip { get; set; }
	public int DisplayOrder { get; set; }
	public bool IsActive { get; set; }
}
