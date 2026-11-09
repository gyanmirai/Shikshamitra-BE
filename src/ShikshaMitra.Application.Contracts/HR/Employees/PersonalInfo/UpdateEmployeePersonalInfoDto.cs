using System.Text.Json.Serialization;
using ShikshaMitra.Common;
using System;

namespace ShikshaMitra.HR.Employees.PersonalInfo;

/// <summary>
/// DTO for updating an existing employee's personal information.
/// </summary>
public class UpdateEmployeePersonalInfoDto
{
	#region Required Fields for Update

	public string? FirstName { get; set; }

	public string? LastName { get; set; }

	public string? MobileNo { get; set; }

	public DateOnly? DateOfJoining { get; set; }

	public Guid RoleId { get; set; }

	#endregion

	#region Optional Fields

	public string? MiddleName { get; set; }

	public string? ShortName { get; set; }

	public EmployeeStatusType EmployeeStatusType { get; set; } = EmployeeStatusType.Active;

	public DateOnly? DateOfBirth { get; set; }

	public string? FamilyCaste { get; set; }

	public CastType? CastType { get; set; }

	public GenderType? GenderType { get; set; }

	public string? Religion { get; set; }

	public MaritalStatusType? MaritalStatus { get; set; }

	public string? BloodGroup { get; set; }

	public string? SecondaryMobileNo { get; set; }

	public string? Email { get; set; }

	public string? PanNo { get; set; }

	public string? AdharNo { get; set; }

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

	public EmployeeGroupType? EmployeeGroup { get; set; }

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

	public string? Address1 { get; set; }

	public string? Address2 { get; set; }

	public string? Address3 { get; set; }

	public Country? Country { get; set; }

	public State? State { get; set; }

	public string? City { get; set; }

	public string? Pincode { get; set; }

	[JsonIgnore] public Guid? ShiftId { get; set; }

	#endregion

}
