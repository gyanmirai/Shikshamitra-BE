using System;

namespace ShikshaMitra.HR.Payroll.Settings;

/// <summary>
/// Full PF rule response DTO.
/// Matches the OpenAPI PfRuleResponse schema.
/// </summary>
public class PfRuleResponseDto
{
	/// <summary>
	/// PF rule unique identifier
	/// </summary>
	public Guid Id { get; set; }

	/// <summary>
	/// Human-readable PF rule name
	/// </summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Unique PF rule code (uppercase)
	/// </summary>
	public string Code { get; set; } = string.Empty;

	/// <summary>
	/// Employee contribution percentage
	/// </summary>
	public decimal EmployeePercentage { get; set; }

	/// <summary>
	/// Employer contribution percentage
	/// </summary>
	public decimal EmployerPercentage { get; set; }

	public PfContributionBasis ContributionBasis { get; set; }

	public decimal? WageCeiling { get; set; }

	public DateTime EffectiveFrom { get; set; }

	public DateTime? EffectiveTo { get; set; }

	public bool IsActive { get; set; }

	public int Version { get; set; }

	/// <summary>
	/// Creation timestamp
	/// </summary>
	public DateTime CreatedAt { get; set; }

	/// <summary>
	/// Last update timestamp (null if never updated)
	/// </summary>
	public DateTime? UpdatedAt { get; set; }
}

