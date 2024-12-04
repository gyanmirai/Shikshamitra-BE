using ShikshaMitra.Common;
using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.Employees;

public class CreateEmployeeBasicInfoDto
{
	public Guid UserId { get; set; }

	// Profile Picture
	public byte[]? ProfilePicture { get; set; }

	public string? LastName { get; set; }
	public string? FirstName { get; set; }
	public string? MiddleName { get; set; }
	public string? ShortName { get; set; }

	public EmployeeStatusType EmployeeStatusType { get; set; }

	public DateOnly? DateOfBirth { get; set; }
	public string? FamilyCast { get; set; }
	public EmployeeCastType? CastType { get; set; }
	public GenderType? Gender { get; set; }
	public string? Religion { get; set; }
	public MaritalStatusType? MaritalStatus { get; set; }
	public string? BloodGroup { get; set; }
	public string? MobileNo { get; set; }

	[EmailAddress] public string? Email { get; set; }

	public string? Department { get; set; }
	public string? Designation { get; set; }
	public DateOnly? DateOfJoining { get; set; }

	public EmployeeGroupType? EmployeeGroup { get; set; }
	public string? Qualification { get; set; }

	[Required(ErrorMessage = "Role is required.")]
	public Guid RoleId { get; set; }

	public string? IdentificationMark1 { get; set; }
	public string? Address { get; set; }
}