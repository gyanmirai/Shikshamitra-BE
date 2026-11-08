using System.Text.Json.Serialization;
using ShikshaMitra.HR.WorkShifts;
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

	private string? _designation;
	/// <summary>Omission preserves the stored value on update; explicit null or empty clears it.</summary>
	public string? Designation
	{
		get => _designation;
		set
		{
			_designation = value;
			DesignationSpecified = true;
		}
	}

	[JsonIgnore]
	public bool DesignationSpecified { get; private set; }

	private string? _qualification;
	/// <summary>Omission preserves the stored value on update; explicit null or empty clears it.</summary>
	public string? Qualification
	{
		get => _qualification;
		set
		{
			_qualification = value;
			QualificationSpecified = true;
		}
	}

	[JsonIgnore]
	public bool QualificationSpecified { get; private set; }

	private string? _identificationMark1;
	/// <summary>Omission preserves the stored value on update; explicit null or empty clears it.</summary>
	public string? IdentificationMark1
	{
		get => _identificationMark1;
		set
		{
			_identificationMark1 = value;
			IdentificationMark1Specified = true;
		}
	}

	[JsonIgnore]
	public bool IdentificationMark1Specified { get; private set; }

	public Guid RoleId { get; set; }

	public InitialWorkShiftInput? InitialWorkShiftAssignment { get; set; }

	public ShiftInfo? Shift { get; set; }

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
	public Guid ShiftId { get; set; }
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

	public List<Guid> OrganizationIds { get; set; } = [];

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
