using ShikshaMitra.Common;
using ShikshaMitra.TenantSettings;
using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Identity;

namespace ShikshaMitra.HR.Employees;

public class EmployeeDto : EntityDto<Guid>
{
	// Basic Details
	public string? EmployeeCode { get; set; }

	public Guid? UserId { get; set; }

	public string? LastName { get; set; }

	public string? FirstName { get; set; }
	public string? MiddleName { get; set; }
	public string? ShortName { get; set; }

	public string? EmployeeNameWithFather { get; set; }
	public string? EmployeeName { get; set; }
	public EmployeeStatusType? EmployeeStatusType { get; set; }

	public DateOnly? DateOfBirth { get; set; }
	public string? FamilyCast { get; set; }
	public CastType? CastType { get; set; }
	public GenderType? GenderType { get; set; }
	public string? Religion { get; set; }
	public MaritalStatusType? MaritalStatus { get; set; }
	public string? BloodGroup { get; set; }
	public string? MobileNo { get; set; }
	public string? SecondaryMobileNo { get; set; }
	public string? Email { get; set; }
	public string? Address1 { get; set; }
	public string? Address2 { get; set; }
	public string? Address3 { get; set; }
	public string? Department { get; set; }
	public string? Designation { get; set; }
	public DateOnly? DateOfJoining { get; set; }

	public EmployeeGroupType? EmployeeGroup { get; set; }
	public string? ChangeReason { get; set; }
	public DateTimeOffset? StatusLastChangedAt { get; set; }

	public string? Qualification { get; set; }
	public Guid RoleId { get; set; }
	public IdentityRoleDto? Role { get; set; }
	public string? IdentificationMark1 { get; set; }
	public string? City { get; set; }
	public State? State { get; set; }
	public Country? Country { get; set; }
	public string? PinCode { get; set; }

	// Value Objects
	public BankDetailsDto BankDetails { get; set; }

	public List<WorkExperienceDto> WorkExperiences { get; set; } = [];

	// Family Members - loaded separately from new table
	public ContactDetailsDto? FatherDetails { get; set; }
	public ContactDetailsDto? MotherDetails { get; set; }
	public ContactDetailsDto? SpouseDetails { get; set; }

	// Profile Picture stored in blob storage
	public string? ProfilePictureBlobName { get; set; }

	public ShiftManagementDto Shift { get; set; }

	public List<Guid> OrganizationIds { get; set; } = [];
	public List<EmployeeOrganizationDto> Organizations { get; set; } = [];
	public List<string> OrganizationNames { get; set; } = [];
}
