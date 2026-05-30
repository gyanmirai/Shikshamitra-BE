using ShikshaMitra.Common;
using ShikshaMitra.TenantSettings;
using ShikshaMitra.HR.Employees;
using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.Employees.PersonalInfo;

/// <summary>
/// DTO for employee personal information response.
/// </summary>
public class EmployeePersonalInfoDto : EntityDto<Guid>
{
	#region Auto-generated Fields

	/// <summary>
	/// Auto-generated employee code.
	/// </summary>
	public string EmployeeCode { get; set; } = string.Empty;

	/// <summary>
	/// Computed full name (FirstName + LastName).
	/// </summary>
	public string FullName { get; set; } = string.Empty;

	public Guid? UserId { get; set; }

	#endregion

	#region Basic Information

	public string FirstName { get; set; } = string.Empty;

	public string LastName { get; set; } = string.Empty;

	public string? MiddleName { get; set; }

	public string? ShortName { get; set; }

	public EmployeeStatusType EmployeeStatusType { get; set; }

	public DateOnly? DateOfBirth { get; set; }

	public string? FamilyCaste { get; set; }

	public CastType? CastType { get; set; }

	public GenderType? GenderType { get; set; }

	public string? Religion { get; set; }

	public MaritalStatusType? MaritalStatus { get; set; }

	public string? BloodGroup { get; set; }

	#endregion

	#region Contact Information

	public string MobileNo { get; set; } = string.Empty;

	public string? SecondaryMobileNo { get; set; }

	public string? Email { get; set; }

	public string PanNo { get; set; } = string.Empty;

	public string AdharNo { get; set; } = string.Empty;

	#endregion

	#region Employment Information

	public string? Department { get; set; }

	public string? Designation { get; set; }

	public DateOnly? DateOfJoining { get; set; }

	public EmployeeGroupType? EmployeeGroup { get; set; }

	public string? Qualification { get; set; }

	public Guid RoleId { get; set; }

	public string? RoleName { get; set; }

	#endregion

	#region Address Information

	public string? IdentificationMark1 { get; set; }

	public string? Address1 { get; set; }

	public string? Address2 { get; set; }

	public string? Address3 { get; set; }

	public Country? Country { get; set; }

	public State? State { get; set; }

	public string? City { get; set; }

	public string? Pincode { get; set; }

	#endregion

	#region Profile Picture

	public string? ProfilePictureBlobName { get; set; }

	#endregion

	#region Related Entities

	public Guid? ShiftId { get; set; }

	public ShiftManagementDto? Shift { get; set; }

	public List<Guid> OrganizationIds { get; set; } = [];

	public List<EmployeeOrganizationDto> Organizations { get; set; } = [];

	public List<string> OrganizationNames { get; set; } = [];

	#endregion
}
