using System;

namespace ShikshaMitra.HR.Payroll.Settings;

/// <summary>
/// DTO for updating a deduction component
/// </summary>
public class UpdateDeductionComponentDto
{
	/// <summary>
	/// Name of the deduction component
	/// </summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Optional description
	/// </summary>
	public string? Description { get; set; }

	/// <summary>
	/// Component type
	/// </summary>
	public DeductionComponentType ComponentType { get; set; }

	/// <summary>
	/// Dependency
	/// </summary>
	public ComponentDependency DependOn { get; set; }

	/// <summary>
	/// Custom component ID when DependOn is CustomComponent
	/// </summary>
	public Guid? CustomComponentId { get; set; }

	/// <summary>
	/// Amount (numeric or percentage)
	/// </summary>
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
