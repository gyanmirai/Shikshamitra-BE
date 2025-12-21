using System.Collections.Generic;

namespace ShikshaMitra.HR.Payroll.Settings;

/// <summary>
/// DTO matching the OpenAPI ComponentListResponse schema for returning a list of earning components.
/// This DTO matches the external API contract for frontend integration.
/// </summary>
public class ComponentListResponseDto
{
	/// <summary>
	/// Total count of items
	/// </summary>
	public int Total { get; set; }
	
	/// <summary>
	/// List of earning components
	/// </summary>
	public List<ComponentResponseDto> Data { get; set; } = new();
}

