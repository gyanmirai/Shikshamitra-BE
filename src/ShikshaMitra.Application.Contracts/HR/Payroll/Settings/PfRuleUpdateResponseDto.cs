using System;

namespace ShikshaMitra.HR.Payroll.Settings;

/// <summary>
/// Response DTO for PF rule update.
/// Matches the OpenAPI PfRuleUpdateResponse schema.
/// </summary>
public class PfRuleUpdateResponseDto
{
	/// <summary>
	/// PF rule ID
	/// </summary>
	public Guid Id { get; set; }
	
	/// <summary>
	/// Last update timestamp
	/// </summary>
	public DateTime UpdatedAt { get; set; }
}

