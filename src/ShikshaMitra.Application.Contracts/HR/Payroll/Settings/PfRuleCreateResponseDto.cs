using System;

namespace ShikshaMitra.HR.Payroll.Settings;

/// <summary>
/// Response DTO for PF rule creation.
/// Matches the OpenAPI PfRuleCreateResponse schema.
/// </summary>
public class PfRuleCreateResponseDto
{
	/// <summary>
	/// Newly created PF rule ID
	/// </summary>
	public Guid Id { get; set; }

	/// <summary>
	/// Creation timestamp
	/// </summary>
	public DateTime CreatedAt { get; set; }
}

