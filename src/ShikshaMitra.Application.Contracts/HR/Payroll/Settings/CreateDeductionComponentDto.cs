using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.Payroll.Settings;

/// <summary>
/// DTO for creating a deduction component
/// </summary>
public class CreateDeductionComponentDto
{
	/// <summary>
	/// Name of the deduction component (must be unique)
	/// </summary>
	[Required]
	[StringLength(DeductionComponentConsts.MaxNameLength, MinimumLength = 1)]
	public string Name { get; set; } = string.Empty;
	
	/// <summary>
	/// Optional description
	/// </summary>
	[StringLength(DeductionComponentConsts.MaxDescriptionLength)]
	public string? Description { get; set; }
	
	/// <summary>
	/// Component type
	/// </summary>
	[Required]
	public DeductionComponentType ComponentType { get; set; }
	
	/// <summary>
	/// Dependency
	/// </summary>
	[Required]
	public ComponentDependency DependOn { get; set; }
	
	/// <summary>
	/// Amount (numeric or percentage)
	/// </summary>
	[Required]
	[StringLength(DeductionComponentConsts.MaxAmountLength, MinimumLength = 1)]
	public string Amount { get; set; } = string.Empty;
	
	/// <summary>
	/// Whether visible in payslip (default: true)
	/// </summary>
	public bool VisibleInPayslip { get; set; } = true;
	
	/// <summary>
	/// Display order (default: 0)
	/// </summary>
	public int DisplayOrder { get; set; } = 0;
}
