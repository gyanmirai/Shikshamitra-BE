using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.Payroll.Settings;

/// <summary>
/// DTO matching the OpenAPI ComponentRequest schema for creating/updating earning components.
/// This DTO matches the external API contract for frontend integration.
/// </summary>
public class ComponentRequestDto
{
	/// <summary>
	/// Name of the earning component (e.g., "Basic Salary", "House Rent Allowance")
	/// Maps to domain property: Name
	/// </summary>
	[Required]
	[StringLength(EarningComponentConsts.MaxNameLength, MinimumLength = 1)]
	public string ComponentName { get; set; } = string.Empty;
	
	/// <summary>
	/// Optional description of the component
	/// </summary>
	[StringLength(EarningComponentConsts.MaxDescriptionLength)]
	public string? Description { get; set; }
	
	/// <summary>
	/// Type of the component as a string (e.g., "Fixed", "Variable")
	/// Maps to domain enum: EarningComponentType
	/// </summary>
	[Required]
	public string ComponentType { get; set; } = string.Empty;
	
	/// <summary>
	/// What this component depends on for calculation (e.g., "None", "Basic", "Gross")
	/// Maps to domain enum: ComponentDependency
	/// </summary>
	public string? DependOn { get; set; }
	
	/// <summary>
	/// Custom component identifier (optional, for extension purposes)
	/// </summary>
	public string? CustomComponent { get; set; }
	
	/// <summary>
	/// Amount value as a number. Will be converted to string for domain storage.
	/// For percentages, use the isFixed=false and provide the percentage value (e.g., 40 for 40%)
	/// </summary>
	[Required]
	public decimal Amount { get; set; }
	
	/// <summary>
	/// Whether this component should be visible in the employee payslip
	/// </summary>
	[Required]
	public bool VisibleInPayslip { get; set; }
	
	/// <summary>
	/// Whether this is a fixed amount component (true) or variable/percentage-based (false)
	/// Maps to domain: ComponentType.Fixed (true) or ComponentType.Variable (false)
	/// </summary>
	[Required]
	public bool IsFixed { get; set; }
}

