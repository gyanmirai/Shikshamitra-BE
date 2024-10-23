using ShikshaMitra.Common;
using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.Employees;

public class EmployeeDto : EntityDto<Guid>
{
	// Basic Details
	public string? Code { get; set; }
	public string? LastName { get; set; }
	public string? FirstName { get; set; }
	public string? MiddleName { get; set; }
	public string? ShortName { get; set; }
	public string? Status { get; set; }

	public DateOnly? DateOfBirth { get; set; }
	public string? FamilyCast { get; set; }
	public CastType? CastType { get; set; }
	public GenderType? Gender { get; set; }
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
	public string? EmployeeGroup { get; set; }
	public string? Qualification { get; set; }
	public string? Role { get; set; }
	public string? IdentificationMark1 { get; set; }
	public string? IdentificationMark2 { get; set; }
	public string? IdentificationMark3 { get; set; }

	// Value Objects
	public ContactDetailsDto FatherDetails { get; set; }
	public ContactDetailsDto MotherDetails { get; set; }
	public ContactDetailsDto SpouseDetails { get; set; }
	public BankDetailsDto BankDetails { get; set; }

	public List<WorkExperienceDto> WorkExperiences { get; set; } = [];

	// Profile Picture
	public byte[]? ProfilePicture { get; set; }
}