using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.Payroll.Settings;

/// <summary>
/// DTO for deduction component
/// </summary>
public class DeductionComponentDto : FullAuditedEntityDto<Guid>
{
	/// <summary>
	/// Name of the deduction component
	/// </summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Description of the component
	/// </summary>
	public string? Description { get; set; }

	/// <summary>
	/// Component type (Fixed or Variable)
	/// </summary>
	public DeductionComponentType ComponentType { get; set; }

	/// <summary>
	/// What this component depends on
	/// </summary>
	public ComponentDependency DependOn { get; set; }

	/// <summary>
	/// Custom component ID when DependOn is CustomComponent
	/// </summary>
	public Guid? CustomComponentId { get; set; }

	/// <summary>
	/// Amount (can be numeric like "1800" or percentage like "10%")
	/// </summary>
	public string Amount { get; set; } = string.Empty;

	/// <summary>
	/// Whether visible in payslip
	/// </summary>
	public bool VisibleInPayslip { get; set; }

	/// <summary>
	/// Display order
	/// </summary>
	public int DisplayOrder { get; set; }

	/// <summary>
	/// Whether component is active
	/// </summary>
	public bool IsActive { get; set; }
}
