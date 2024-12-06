using ShikshaMitra.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Employees;

public class Employee : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid UserId { get; private set; }

	// Profile Picture
	public byte[]? ProfilePicture { get; private set; }

	// Basic Details
	public string EmployeeCode { get; private set; }
	public string? LastName { get; private set; }
	public string? FirstName { get; private set; }
	public string? MiddleName { get; private set; }
	public string? ShortName { get; private set; }

	public EmployeeStatusType? EmployeeEmployeeStatusTypeType { get; set; }

	public DateOnly? DateOfBirth { get; private set; }
	public string? FamilyCast { get; private set; }
	public EmployeeCastType? CastType { get; private set; }
	public GenderType? GenderType { get; private set; }
	public string? Religion { get; private set; }
	public MaritalStatusType? MaritalStatus { get; private set; }
	public string? BloodGroup { get; private set; }
	public string? MobileNo { get; private set; }
	public string? Email { get; private set; }
	public string? Address { get; private set; }
	public string? Department { get; private set; }
	public string? Designation { get; private set; }
	public DateOnly? DateOfJoining { get; private set; }
	public EmployeeGroupType? EmployeeGroup { get; private set; }
	public string? Qualification { get; private set; }
	public Guid? RoleId { get; private set; }
	public string? IdentificationMark1 { get; private set; }

	// Value Objects
	public ContactDetails FatherDetails { get; private set; }
	public ContactDetails MotherDetails { get; private set; }
	public ContactDetails SpouseDetails { get; private set; }
	public BankDetails BankDetails { get; private set; }

	public List<WorkExperience> WorkExperiences { get; private set; } = new List<WorkExperience>();
	public List<EmployeeDocument> Documents { get; private set; } = new List<EmployeeDocument>();

	// Full name including middle name
	public string EmployeeNameWithFather
	{
		get
		{
			var names = new[] { FirstName, MiddleName, LastName }
				.Where(x => !string.IsNullOrWhiteSpace(x));
			return string.Join(" ", names);
		}
	}

	// Full name without middle name
	public string EmployeeName
	{
		get
		{
			var names = new[] { FirstName, LastName }
				.Where(x => !string.IsNullOrWhiteSpace(x));
			return string.Join(" ", names);
		}
	}

	// Constructor for creating a new employee
	private Employee() { } // For ORM

	internal Employee(
		Guid id,
		Guid? tenantId,
		Guid userId,
		byte[]? profilePicture,
		string? employeeCode,
		string? lastName,
		string? firstName,
		string? middleName,
		string? shortName,
		EmployeeStatusType? employeeStatusType,
		DateOnly? dateOfBirth,
		string? familyCast,
		EmployeeCastType? castType,
		GenderType? genderType,
		string? religion,
		MaritalStatusType? maritalStatus,
		string? bloodGroup,
		string? mobileNo,
		string? email,
		string? address,
		string? department,
		string? designation,
		DateOnly? dateOfJoining,
		EmployeeGroupType? employeeGroup,
		string? qualification,
		Guid? roleId,
		string? identificationMark1,
		ContactDetails? fatherDetails,
		ContactDetails? motherDetails,
		ContactDetails? spouseDetails,
		BankDetails? bankDetails,
		List<WorkExperience>? workExperiences
	) : base(id)
	{
		TenantId = tenantId;
		UserId = userId;
		ProfilePicture = profilePicture;
		EmployeeCode = employeeCode;
		LastName = lastName;
		FirstName = firstName;
		MiddleName = middleName;
		ShortName = shortName;
		EmployeeEmployeeStatusTypeType = employeeStatusType;
		DateOfBirth = dateOfBirth;
		FamilyCast = familyCast;
		CastType = castType;
		GenderType = genderType;
		Religion = religion;
		MaritalStatus = maritalStatus;
		BloodGroup = bloodGroup;
		MobileNo = mobileNo;
		Email = email;
		Address = address;
		Department = department;
		Designation = designation;
		DateOfJoining = dateOfJoining;
		EmployeeGroup = employeeGroup;
		Qualification = qualification;
		RoleId = roleId;
		IdentificationMark1 = identificationMark1;
		FatherDetails = fatherDetails;
		MotherDetails = motherDetails;
		SpouseDetails = spouseDetails;
		BankDetails = bankDetails;
		WorkExperiences = workExperiences ?? [];
	}

	internal Employee UpdateBasicInfo(
		string? lastName,
		string? firstName,
		string? middleName,
		string? shortName,
		EmployeeStatusType? employeeStatusType,
		DateOnly? dateOfBirth,
		byte[]? profilePicture,
		string? familyCast,
		EmployeeCastType? castType,
		GenderType? genderType,
		string? religion,
		MaritalStatusType? maritalStatus,
		string? bloodGroup,
		string? mobileNo,
		string? email,
		string? address,
		string? department,
		string? designation,
		DateOnly? dateOfJoining,
		EmployeeGroupType? employeeGroup,
		string? qualification,
		Guid? roleId,
		string? identificationMark1)
	{
		LastName = lastName;
		FirstName = firstName;
		MiddleName = middleName;
		ShortName = shortName;
		EmployeeEmployeeStatusTypeType = employeeStatusType;
		DateOfBirth = dateOfBirth;
		ProfilePicture = profilePicture;
		FamilyCast = familyCast;
		CastType = castType;
		GenderType = genderType;
		Religion = religion;
		MaritalStatus = maritalStatus;
		BloodGroup = bloodGroup;
		MobileNo = mobileNo;
		Email = email;
		Address = address;
		Department = department;
		Designation = designation;
		DateOfJoining = dateOfJoining;
		EmployeeGroup = employeeGroup;
		Qualification = qualification;
		RoleId = roleId;
		IdentificationMark1 = identificationMark1;
		
		return this;
	}

	internal Employee UpdateFamilyDetails(
		ContactDetails fatherDetails,
		ContactDetails motherDetails,
		ContactDetails spouseDetails)
	{
		this.FatherDetails = fatherDetails;
		this.MotherDetails = motherDetails;
		this.SpouseDetails = spouseDetails;

		return this;
	}

	internal Employee UpdateWorkExperiences(List<WorkExperience> workExperiences)
	{
		this.WorkExperiences = workExperiences;

		return this;
	}

	internal Employee UpdateBankDetails(BankDetails bankDetails)
	{
		this.BankDetails = bankDetails;

		return this;
	}

	internal void AddDocument(EmployeeDocument document) => Documents.Add(document);
}