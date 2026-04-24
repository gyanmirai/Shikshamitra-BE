using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.Employees.FamilyInfo;

/// <summary>
/// Request DTO for creating or updating employee family information
/// </summary>
public class FamilyInfoRequest
{
	public Guid? Id { get; set; }

	public FamilyMemberInfo? FatherDetails { get; set; }

	public FamilyMemberInfo? MotherDetails { get; set; }

	public FamilyMemberInfo? SpouseDetails { get; set; }
}

/// <summary>
/// Response DTO for employee family information
/// </summary>
public class FamilyInfoResponse
{
	[Required]
	public Guid Id { get; set; }

	public FamilyMemberInfo? FatherDetails { get; set; }

	public FamilyMemberInfo? MotherDetails { get; set; }

	public FamilyMemberInfo? SpouseDetails { get; set; }
}

/// <summary>
/// Family member details
/// </summary>
public class FamilyMemberInfo
{
	[MaxLength(256)]
	public string? Name { get; set; }

	[MaxLength(256)]
	public string? Occupation { get; set; }

	// Max 20 matches domain rules; [Phone] rejects common Indian mobile formats on model binding.
	[MaxLength(20)]
	public string? MobileNo { get; set; }

	[MaxLength(512)]
	public string? Address1 { get; set; }

	[MaxLength(512)]
	public string? Address2 { get; set; }

	[MaxLength(512)]
	public string? Address3 { get; set; }

	public CountryInfo? Country { get; set; }

	public StateInfo? State { get; set; }

	[MaxLength(128)]
	public string? City { get; set; }

	[MaxLength(10)]
	public string? Pincode { get; set; }
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
