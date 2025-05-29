using ShikshaMitra.Common;
using ShikshaMitra.HR.Payroll.PayrollStructures;
using ShikshaMitra.TenantSettings;
using ShikshaMitra.TenantSettings.EducationBoards;
using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Employees;

public class Employee : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid? UserId { get; private set; }
	public Guid EducationMediumStageId { get; private set; }

	// Profile Picture
	public byte[]? ProfilePicture { get; private set; }

	// Basic Details
	public string EmployeeCode { get; private set; }
	public string? LastName { get; private set; }
	public string? FirstName { get; private set; }
	public string? MiddleName { get; private set; }
	public string? ShortName { get; private set; }

	public EmployeeStatusType? EmployeeStatusType { get; private set; }
	public string? ChangeReason { get; private set; }
	public DateTimeOffset? StatusLastChangedAt { get; private set; }
	public DateOnly? DateOfBirth { get; private set; }
	public string? FamilyCast { get; private set; }
	public CastType? CastType { get; private set; }
	public GenderType? GenderType { get; private set; }
	public string? Religion { get; private set; }
	public MaritalStatusType? MaritalStatus { get; private set; }
	public string? BloodGroup { get; private set; }
	public string? MobileNo { get; private set; }
	public string? Email { get; private set; }
	public string? Address1 { get; private set; }
	public string? Address2 { get; private set; }
	public string? Address3 { get; private set; }
	public string? Department { get; private set; }
	public string? Designation { get; private set; }
	public DateOnly? DateOfJoining { get; private set; }
	public EmployeeGroupType? EmployeeGroup { get; private set; }
	public string? Qualification { get; private set; }
	public Guid RoleId { get; private set; }
	public string? IdentificationMark1 { get; private set; }

	public string? City { get; private set; }
	public State? State { get; private set; }
	public Country? Country { get; private set; }

	public string? PinCode { get; private set; }

	// Value Objects
	public ContactDetails FatherDetails { get; private set; }
	public ContactDetails MotherDetails { get; private set; }
	public ContactDetails SpouseDetails { get; private set; }
	public BankDetails BankDetails { get; private set; }
	public Guid? ShiftId { get; set; }

	public List<WorkExperience> WorkExperiences { get; private set; } = new List<WorkExperience>();
	public List<EmployeeDocument> Documents { get; private set; } = new List<EmployeeDocument>();

	// Payroll Structure
	public Guid? PayrollStructureId { get; private set; }

	// Navigation Property for Role
	public virtual IdentityRole? Role { get; private set; }

	// Navigation Property for EducationMediumStage
	public virtual EducationMediumStage? EducationMediumStage { get; private set; }

	// Navigation property for PayrollStructure
	public virtual PayrollStructure? PayrollStructure { get; private set; }
	// Navigation property for Shift
	public virtual ShiftManagement? Shift { get; private set; }

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
		Guid? userId,
		Guid educationMediumStageId,
		byte[]? profilePicture,
		string? employeeCode,
		string? lastName,
		string? firstName,
		string? middleName,
		string? shortName,
		EmployeeStatusType? employeeStatusType,
		DateOnly? dateOfBirth,
		string? familyCast,
		CastType? castType,
		GenderType? genderType,
		string? religion,
		MaritalStatusType? maritalStatus,
		string? bloodGroup,
		string? mobileNo,
		string? email,
		string? address1,
		string? address2,
		string? address3,
		string? department,
		string? designation,
		DateOnly? dateOfJoining,
		EmployeeGroupType? employeeGroup,
		string? qualification,
		Guid? roleId,
		string? identificationMark1,
		string? city,
		State? state,
		Country? country,
		string? pinCode,
		ContactDetails? fatherDetails,
		ContactDetails? motherDetails,
		ContactDetails? spouseDetails,
		BankDetails? bankDetails,
		List<WorkExperience>? workExperiences,
		Guid? shiftId
	) : base(id)
	{
		TenantId = tenantId;
		UserId = userId;
		EducationMediumStageId = educationMediumStageId;
		ProfilePicture = profilePicture;
		EmployeeCode = employeeCode;
		LastName = lastName;
		FirstName = firstName;
		MiddleName = middleName;
		ShortName = shortName;
		EmployeeStatusType = employeeStatusType;
		DateOfBirth = dateOfBirth;
		FamilyCast = familyCast;
		CastType = castType;
		GenderType = genderType;
		Religion = religion;
		MaritalStatus = maritalStatus;
		BloodGroup = bloodGroup;
		MobileNo = mobileNo;
		Email = email;
		Address1 = address1;
		Address2 = address2;
		Address3 = address3;
		Department = department;
		Designation = designation;
		DateOfJoining = dateOfJoining;
		EmployeeGroup = employeeGroup;
		Qualification = qualification;
		RoleId = Check.NotNull(roleId, nameof(roleId)) ?? throw new ArgumentNullException(nameof(roleId));
		IdentificationMark1 = identificationMark1;
		City = city;
		State = state;
		Country = country;
		PinCode = pinCode;
		FatherDetails = fatherDetails;
		MotherDetails = motherDetails;
		SpouseDetails = spouseDetails;
		BankDetails = bankDetails;
		WorkExperiences = workExperiences ?? [];
		ShiftId = shiftId;
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
		CastType? castType,
		GenderType? genderType,
		string? religion,
		MaritalStatusType? maritalStatus,
		string? bloodGroup,
		string? mobileNo,
		string? email,
		string? address1,
		string? address2,
		string? address3,
		string? department,
		string? designation,
		DateOnly? dateOfJoining,
		EmployeeGroupType? employeeGroup,
		string? qualification,
		Guid? roleId,
		string? identificationMark1,
		string? city,
		State? state,
		Country? country,
		string? pinCode,
		Guid educationMediumStageId,
		Guid? shiftId
		)
	{
		LastName = lastName;
		FirstName = firstName;
		MiddleName = middleName;
		ShortName = shortName;
		EmployeeStatusType = employeeStatusType;
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
		Address1 = address1;
		Address2 = address2;
		Address3 = address3;
		Department = department;
		Designation = designation;
		DateOfJoining = dateOfJoining;
		EmployeeGroup = employeeGroup;
		Qualification = qualification;
		RoleId = Check.NotNull(roleId, nameof(roleId)) ?? throw new ArgumentNullException(nameof(roleId));
		IdentificationMark1 = identificationMark1;
		City = city;
		State = state;
		Country = country;
		PinCode = pinCode;
		EducationMediumStageId = educationMediumStageId;
		ShiftId = shiftId;
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

	internal Employee UpdateEmployeeStatus(EmployeeStatusType employeeStatusType, string changeReason)
	{
		EmployeeStatusType = employeeStatusType;
		ChangeReason = changeReason;
		StatusLastChangedAt = DateTimeOffset.Now;

		return this;
	}

	internal void AddDocument(EmployeeDocument document) => Documents.Add(document);

	internal void SetPayrollStructure(Guid payrollStructureId)
	{
		PayrollStructureId = payrollStructureId;
	}

	internal void RemovePayrollStructure()
	{
		PayrollStructureId = null;
	}

	internal void UpdateUserId(Guid id)
	{
		UserId = id;
	}
}