using ShikshaMitra.Common;
using System;

namespace ShikshaMitra.HR.Employees.PersonalInfo;

/// <summary>
/// DTO for creating a new employee with personal information.
/// </summary>
public class CreateEmployeePersonalInfoDto
{
	public string FirstName { get; set; } = string.Empty;

	public string LastName { get; set; } = string.Empty;

	public string MobileNo { get; set; } = string.Empty;

	public DateOnly DateOfJoining { get; set; }

	public Guid RoleId { get; set; }

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

	public string PanNo { get; set; } = string.Empty;

	public string AdharNo { get; set; } = string.Empty;

	public string? Department { get; set; }

	public string? Designation { get; set; }

	public EmployeeGroupType? EmployeeGroup { get; set; }

	public string? Qualification { get; set; }

	public string? IdentificationMark1 { get; set; }

	public string? Address1 { get; set; }

	public string? Address2 { get; set; }

	public string? Address3 { get; set; }

	public Country? Country { get; set; }

	public State? State { get; set; }

	public string? City { get; set; }

	public string? Pincode { get; set; }

	public Guid? ShiftId { get; set; }

	public CreateEmployeePolicyOverrideDto? PolicyOverride { get; set; }

}

public class CreateEmployeePolicyOverrideDto
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
