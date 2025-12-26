using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.Payroll.Settings;

/// <summary>
/// DTO for creating or updating a PF rule.
/// Matches the OpenAPI PfRuleCreateRequest schema.
/// </summary>
public class CreateUpdateProvidentFundDto
{
	/// <summary>
	/// Human-readable PF rule name
	/// </summary>
	[Required]
	[MinLength(2)]
	[MaxLength(100)]
	public string Name { get; set; } = string.Empty;
	
	/// <summary>
	/// Unique PF rule code (uppercase letters and underscores only)
	/// </summary>
	[Required]
	[MinLength(2)]
	[MaxLength(20)]
	[RegularExpression(@"^[A-Z_]+$", ErrorMessage = "Code must contain only uppercase letters and underscores")]
	public string Code { get; set; } = string.Empty;
	
	/// <summary>
	/// Employee contribution percentage (0-100)
	/// </summary>
	[Required]
	[Range(0, 100, ErrorMessage = "Employee percentage must be between 0 and 100")]
	public decimal EmployeePercentage { get; set; }
	
	/// <summary>
	/// Employer contribution percentage (0-100)
	/// </summary>
	[Required]
	[Range(0, 100, ErrorMessage = "Employer percentage must be between 0 and 100")]
	public decimal EmployerPercentage { get; set; }
}