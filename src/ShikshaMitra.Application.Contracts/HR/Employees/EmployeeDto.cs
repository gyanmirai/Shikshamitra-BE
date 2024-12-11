using ShikshaMitra.Common;
using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.Employees;

public class EmployeeDto : EntityDto<Guid>
{
	// Basic Details
	public string? EmployeeCode { get; set; }

	public string? LastName { get; set; }
	public string? FirstName { get; set; }
	public string? MiddleName { get; set; }
	public string? ShortName { get; set; }

	public string? EmployeeNameWithFather { get; set; }
	public string? EmployeeName { get; set; }
	public EmployeeStatusType? EmployeeStatusType { get; set; }

	public DateOnly? DateOfBirth { get; set; }
	public string? FamilyCast { get; set; }
	public EmployeeCastType? CastType { get; set; }
	public GenderType? GenderType { get; set; }
	public string? Religion { get; set; }
	public MaritalStatusType? MaritalStatus { get; set; }
	public string? BloodGroup { get; set; }
	public string? MobileNo { get; set; }
	public string? Email { get; set; }
	public string? Address { get; set; }
	public string? Department { get; set; }
	public string? Designation { get; set; }
	public DateOnly? DateOfJoining { get; set; }
	public string? Photo { get; set; }
	public EmployeeGroupType? EmployeeGroup { get; private set; }
	public string? Qualification { get; set; }
	public string? Role { get; set; }
	public string? IdentificationMark1 { get; set; }

	// Value Objects
	public ContactDetailsDto FatherDetails { get; set; }
	public ContactDetailsDto MotherDetails { get; set; }
	public ContactDetailsDto SpouseDetails { get; set; }
	public BankDetailsDto BankDetails { get; set; }

	public List<WorkExperienceDto> WorkExperiences { get; set; } = [];

	// Profile Picture
	public byte[]? ProfilePicture { get; set; }
}