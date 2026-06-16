using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ShikshaMitra.HR.Employees;

namespace ShikshaMitra.HR.Employees.PersonalInfo;

/// <summary>
/// Request DTO for creating or updating employee personal information
/// </summary>
public class PersonalInfoRequest
{
	public string FirstName { get; set; } = string.Empty;

	public string? MiddleName { get; set; }

	public string LastName { get; set; } = string.Empty;

	public string? ShortName { get; set; }

	public DateTime? Dob { get; set; }

	/// <summary>
	/// Gender: 1 = Male, 2 = Female
	/// </summary>
	public int? Gender { get; set; }

	public string? Religion { get; set; }

	/// <summary>
	/// Caste Type: 0 = General, 1 = OBC, 2 = SC, 3 = ST
	/// </summary>
	public int? CasteType { get; set; }

	public string? FamilyCaste { get; set; }

	/// <summary>
	/// Marital Status: 0 = Unmarried, 1 = Married, 2 = Divorced, 3 = Widowed
	/// </summary>
	public int? MaritalStatus { get; set; }

	/// <summary>
	/// Blood Group: 1 = A+, 2 = A-, 3 = B+, 4 = B-, 5 = AB+, 6 = AB-, 7 = O+, 8 = O-
	/// </summary>
	public int? BloodGroup { get; set; }

	public string? Email { get; set; }

	public string MobileNo { get; set; } = string.Empty;

	public string? SecondaryMobileNo { get; set; }

	public string PanNo { get; set; } = string.Empty;

	public string AdharNo { get; set; } = string.Empty;

	public DateTime Doj { get; set; }

	public bool IsTeaching { get; set; }

	public string? Department { get; set; }

	public string? Designation { get; set; }

	public string? Qualification { get; set; }

	public string? IdentificationMark1 { get; set; }

	public Guid RoleId { get; set; }

	public Guid ShiftId { get; set; }

	public List<Guid> OrganizationIds { get; set; } = [];

	public string? Address1 { get; set; }

	public string? Address2 { get; set; }

	public string? Address3 { get; set; }

	public CountryInfo? Country { get; set; }

	public StateInfo? State { get; set; }

	public string? City { get; set; }

	public string? PinCode { get; set; }

	public PersonalInfoPolicyOverrideRequest? PolicyOverride { get; set; }
}

public class PersonalInfoPolicyOverrideRequest
{
	public Guid AcademicYearId { get; set; }
	public Guid AttendancePolicyId { get; set; }
	public Guid PayrollPolicyId { get; set; }
	public Guid LeavePolicyId { get; set; }
	public DateTime EffectiveFrom { get; set; }
	public DateTime? EffectiveTo { get; set; }
	public string? AssignmentReason { get; set; }
	public string? ConfirmationToken { get; set; }
}

/// <summary>
/// Response DTO for employee personal information
/// </summary>
public class PersonalInfoResponse : PersonalInfoRequest
{
	[Required]
	public Guid Id { get; set; }

	[Required]
	public string EmployeeCode { get; set; } = string.Empty;

	[Required]
	public string FullName { get; set; } = string.Empty;

	public Guid? UserId { get; set; }

	/// <summary>
	/// True when the employee has a profile picture stored in blob storage.
	/// Use GET /api/employees/{id}/profile-picture to download.
	/// </summary>
	public bool HasProfilePicture { get; set; }

	public List<EmployeeOrganizationDto> Organizations { get; set; } = [];

	public List<string> OrganizationNames { get; set; } = [];
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
