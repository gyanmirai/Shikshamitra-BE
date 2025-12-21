using System;

namespace ShikshaMitra.HR.Payroll.Settings;

/// <summary>
/// DTO matching the OpenAPI ComponentResponse schema for returning earning component details.
/// This DTO matches the external API contract for frontend integration.
/// </summary>
public class ComponentResponseDto
{
	/// <summary>
	/// Unique identifier of the earning component
	/// </summary>
	public Guid Id { get; set; }
	
	/// <summary>
	/// Name of the earning component (e.g., "Basic Salary", "House Rent Allowance")
	/// Maps from domain property: Name
	/// </summary>
	public string ComponentName { get; set; } = string.Empty;
	
	/// <summary>
	/// Optional description of the component
	/// </summary>
	public string? Description { get; set; }
	
	/// <summary>
	/// Type of the component as a string (e.g., "Fixed", "Variable")
	/// Maps from domain enum: EarningComponentType
	/// </summary>
	public string ComponentType { get; set; } = string.Empty;
	
	/// <summary>
	/// What this component depends on for calculation (e.g., "None", "Basic", "Gross")
	/// Maps from domain enum: ComponentDependency
	/// </summary>
	public string? DependOn { get; set; }
	
	/// <summary>
	/// Custom component identifier (optional, for extension purposes)
	/// </summary>
	public string? CustomComponent { get; set; }
	
	/// <summary>
	/// Amount value as a number.
	/// For percentages, this will be the percentage value (e.g., 40 for 40%)
	/// </summary>
	public decimal Amount { get; set; }
	
	/// <summary>
	/// Whether this component is visible in the employee payslip
	/// </summary>
	public bool VisibleInPayslip { get; set; }
	
	/// <summary>
	/// Whether this is a fixed amount component (true) or variable/percentage-based (false)
	/// Maps from domain: ComponentType.Fixed (true) or ComponentType.Variable (false)
	/// </summary>
	public bool IsFixed { get; set; }
}

