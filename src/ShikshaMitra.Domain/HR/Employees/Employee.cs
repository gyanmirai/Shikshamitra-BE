using ShikshaMitra.Common;
using ShikshaMitra.Students;
using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Employees;

public class Employee : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid UserId { get; private set; }

	// Basic Details
	public string? OrganizationName { get; private set; }
	public string? Code { get; private set; }
	public string? LastName { get; private set; }
	public string? FirstName { get; private set; }
	public string? MiddleName { get; private set; }
	public string? ShortName { get; private set; }
	public string? Status { get; private set; }

	public DateOnly? DateOfBirth { get; private set; }
	public string? FamilyCast { get; private set; }
	public CastType? CastType { get; private set; }
	public GenderType? Gender { get; private set; }
	public string? Religion { get; private set; }
	public MaritalStatusType? MaritalStatus { get; private set; }
	public string? BloodGroup { get; private set; }
	public string? MobileNo { get; private set; }
	public string? Email { get; private set; }
	public string? Address { get; private set; }
	public string? Department { get; private set; }
	public string? Designation { get; private set; }
	public DateOnly? DateOfJoining { get; private set; }
	public string? Photo { get; private set; }
	public string? EmployeeGroup { get; private set; }
	public string? Qualification { get; private set; }
	public string? Role { get; private set; }
	public string? IdentificationMark1 { get; private set; }
	public string? IdentificationMark2 { get; private set; }
	public string? IdentificationMark3 { get; private set; }

	// Value Objects
	public ContactDetails FatherDetails { get; private set; }
	public ContactDetails MotherDetails { get; private set; }
	public ContactDetails SpouseDetails { get; private set; }
	public BankDetails BankDetails { get; private set; }

	public List<WorkExperience> WorkExperiences { get; private set; } = new List<WorkExperience>();

	// Profile Picture
	public byte[]? ProfilePicture { get; private set; }

	// Constructor for creating a new employee
	private Employee() { } // For ORM

	internal Employee(
		Guid id,
		Guid? tenantId,
		Guid userId,
		string? organizationName,
		string? code,
		string? lastName,
		string? firstName,
		string? middleName,
		string? shortName,
		string? status,
		DateOnly? dateOfBirth,
		string? familyCast,
		CastType? castType,
		GenderType? gender,
		string? religion,
		MaritalStatusType? maritalStatus,
		string? bloodGroup,
		string? mobileNo,
		string? email,
		string? address,
		string? department,
		string? designation,
		DateOnly? dateOfJoining,
		string? photo,
		string? employeeGroup,
		string? qualification,
		string? role,
		string? identificationMark1,
		string? identificationMark2,
		string? identificationMark3,
		ContactDetails? fatherDetails,
		ContactDetails? motherDetails,
		ContactDetails? spouseDetails,
		BankDetails? bankDetails,
		List<WorkExperience>? workExperiences,
		byte[]? profilePicture = null
	) : base(id)
	{
		TenantId = tenantId;
		UserId = userId;
		OrganizationName = organizationName;
		Code = code;
		LastName = lastName;
		FirstName = firstName;
		MiddleName = middleName;
		ShortName = shortName;
		Status = status;
		DateOfBirth = dateOfBirth;
		FamilyCast = familyCast;
		CastType = castType;
		Gender = gender;
		Religion = religion;
		MaritalStatus = maritalStatus;
		BloodGroup = bloodGroup;
		MobileNo = mobileNo;
		Email = email;
		Address = address;
		Department = department;
		Designation = designation;
		DateOfJoining = dateOfJoining;
		Photo = photo;
		EmployeeGroup = employeeGroup;
		Qualification = qualification;
		Role = role;
		IdentificationMark1 = identificationMark1;
		IdentificationMark2 = identificationMark2;
		IdentificationMark3 = identificationMark3;
		FatherDetails = fatherDetails;
		MotherDetails = motherDetails;
		SpouseDetails = spouseDetails;
		BankDetails = bankDetails;
		WorkExperiences = workExperiences ?? new List<WorkExperience>();
		ProfilePicture = profilePicture;
	}
}