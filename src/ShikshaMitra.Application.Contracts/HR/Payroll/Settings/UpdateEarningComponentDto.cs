using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.Payroll.Settings;

/// <summary>
/// DTO for updating an earning component
/// </summary>
public class UpdateEarningComponentDto
{
	/// <summary>
	/// Name of the earning component
	/// </summary>
	[Required]
	[StringLength(EarningComponentConsts.MaxNameLength, MinimumLength = 1)]
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Optional description
	/// </summary>
	[StringLength(EarningComponentConsts.MaxDescriptionLength)]
	public string? Description { get; set; }

	/// <summary>
	/// Component type
	/// </summary>
	[Required]
	public EarningComponentType ComponentType { get; set; }

	/// <summary>
	/// Dependency
	/// </summary>
	[Required]
	public ComponentDependency DependOn { get; set; }

	/// <summary>
	/// Custom component ID when DependOn is CustomComponent
	/// </summary>
	public Guid? CustomComponentId { get; set; }

	/// <summary>
	/// Amount (numeric or percentage)
	/// </summary>
	[Required]
	[StringLength(EarningComponentConsts.MaxAmountLength, MinimumLength = 1)]
	public string Amount { get; set; } = string.Empty;

	/// <summary>
	/// Whether visible in payslip
	/// </summary>
	public bool VisibleInPayslip { get; set; } = true;

	/// <summary>
	/// Display order
	/// </summary>
	public int DisplayOrder { get; set; } = 0;
}
