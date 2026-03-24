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
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Unique PF rule code (uppercase letters and underscores only)
	/// </summary>
	public string Code { get; set; } = string.Empty;

	/// <summary>
	/// Employee contribution percentage (0-100)
	/// </summary>
	public decimal EmployeePercentage { get; set; }

	/// <summary>
	/// Employer contribution percentage (0-100)
	/// </summary>
	public decimal EmployerPercentage { get; set; }
}
