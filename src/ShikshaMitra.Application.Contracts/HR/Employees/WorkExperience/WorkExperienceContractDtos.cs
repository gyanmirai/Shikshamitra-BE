using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.Employees.WorkExperience;

/// <summary>
/// Request/Response DTO for employee work experience
/// </summary>
public class WorkExperienceContract
{
	public Guid? Id { get; set; }

	[MaxLength(256)]
	public string? MediumStageName { get; set; }

	[MaxLength(256)]
	public string? Designation { get; set; }

	[MaxLength(256)]
	public string? Department { get; set; }

	public CountryInfo? Country { get; set; }

	public StateInfo? State { get; set; }

	[MaxLength(128)]
	public string? City { get; set; }

	public DateTime? StartDate { get; set; }

	public DateTime? EndDate { get; set; }
}

/// <summary>
/// Country information
/// </summary>
public class CountryInfo
{
	[Required]
	public string Name { get; set; } = string.Empty;

	[Required]
	public string IsoCode { get; set; } = string.Empty;
}

/// <summary>
/// State information
/// </summary>
public class StateInfo
{
	[Required]
	public string Name { get; set; } = string.Empty;

	[Required]
	public string IsoCode { get; set; } = string.Empty;
}
