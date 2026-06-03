using ShikshaMitra.Common;
using ShikshaMitra.TenantSettings;
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
	public string? ProfilePictureBlobName { get; private set; }
	public string EmployeeCode { get; private set; }
	public string? LastName { get; private set; }
	public string? FirstName { get; private set; }
	public string? MiddleName { get; private set; }
	public string? ShortName { get; private set; }
	public EmployeeStatusType? EmployeeStatusType { get; private set; }
	public string? ChangeReason { get; private set; }
	public DateTimeOffset? StatusLastChangedAt { get; private set; }
	public DateOnly? StatusEffectiveDate { get; private set; }
	public DateOnly? DateOfBirth { get; private set; }
	public string? FamilyCast { get; private set; }
	public CastType? CastType { get; private set; }
	public GenderType? GenderType { get; private set; }
	public string? Religion { get; private set; }
	public MaritalStatusType? MaritalStatus { get; private set; }
	public string? BloodGroup { get; private set; }
	public string? MobileNo { get; private set; }
	public string? SecondaryMobileNo { get; private set; }
	public string? Email { get; private set; }
	public string PanNo { get; private set; }
	public string AdharNo { get; private set; }
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
	public BankDetails BankDetails { get; private set; }
	public Guid? ShiftId { get; set; }
	public List<WorkExperience> WorkExperiences { get; private set; } = [];
	public List<EmployeeDocument> Documents { get; private set; } = [];
	public virtual ICollection<EmployeeFamilyMember> FamilyMembers { get; private set; } = new List<EmployeeFamilyMember>();
	public virtual ICollection<EmployeeOrganizationAssignment> OrganizationAssignments { get; private set; } = new List<EmployeeOrganizationAssignment>();
	public Guid? ReportingManagerId { get; private set; }
	public virtual IdentityRole? Role { get; private set; }
	public virtual ShiftManagement? Shift { get; private set; }
	public virtual Employee? ReportingManager { get; private set; }
	public virtual EmployeeAdditionalDetails? AdditionalDetails { get; private set; }

	public string EmployeeNameWithFather => JoinNameParts(FirstName, MiddleName, LastName);
	public string EmployeeName => JoinNameParts(FirstName, LastName);

	private Employee()
	{
	}

	internal Employee(
		Guid id,
		Guid? tenantId,
		Guid? userId,
		string? employeeCode,
		EmployeeBasicInfo basicInfo,
		BankDetails? bankDetails,
		List<WorkExperience>? workExperiences) : base(id)
	{
		TenantId = tenantId;
		UserId = userId;
		EmployeeCode = employeeCode;
		UpdateBasicInfo(basicInfo);
		BankDetails = bankDetails;
		WorkExperiences = workExperiences ?? [];
	}

	internal Employee UpdateBasicInfo(EmployeeBasicInfo basicInfo)
	{
		ProfilePictureBlobName = basicInfo.ProfilePictureBlobName;
		LastName = basicInfo.LastName;
		FirstName = basicInfo.FirstName;
		MiddleName = basicInfo.MiddleName;
		ShortName = basicInfo.ShortName;
		EmployeeStatusType = basicInfo.EmployeeStatusType;
		DateOfBirth = basicInfo.DateOfBirth;
		FamilyCast = basicInfo.FamilyCast;
		CastType = basicInfo.CastType;
		GenderType = basicInfo.GenderType;
		Religion = basicInfo.Religion;
		MaritalStatus = basicInfo.MaritalStatus;
		BloodGroup = basicInfo.BloodGroup;
		MobileNo = basicInfo.MobileNo;
		SecondaryMobileNo = basicInfo.SecondaryMobileNo;
		Email = basicInfo.Email;
		PanNo = Check.NotNullOrWhiteSpace(basicInfo.PanNo, nameof(basicInfo.PanNo)).ToUpperInvariant();
		AdharNo = Check.NotNullOrWhiteSpace(basicInfo.AdharNo, nameof(basicInfo.AdharNo));
		Address1 = basicInfo.Address1;
		Address2 = basicInfo.Address2;
		Address3 = basicInfo.Address3;
		Department = basicInfo.Department;
		Designation = basicInfo.Designation;
		DateOfJoining = basicInfo.DateOfJoining;
		EmployeeGroup = basicInfo.EmployeeGroup;
		Qualification = basicInfo.Qualification;
		RoleId = Check.NotNull(basicInfo.RoleId, nameof(basicInfo.RoleId)) ?? throw new ArgumentNullException(nameof(basicInfo.RoleId));
		IdentificationMark1 = basicInfo.IdentificationMark1;
		City = basicInfo.City;
		State = Clone(basicInfo.State);
		Country = Clone(basicInfo.Country);
		PinCode = basicInfo.PinCode;
		ShiftId = basicInfo.ShiftId;
		return this;
	}

	internal Employee UpdateWorkExperiences(List<WorkExperience> workExperiences)
	{
		ArgumentNullException.ThrowIfNull(workExperiences);
		WorkExperiences.Clear();
		foreach (var item in workExperiences)
		{
			WorkExperiences.Add(item);
		}

		return this;
	}

	internal Employee UpdateBankDetails(BankDetails bankDetails)
	{
		BankDetails = bankDetails;
		return this;
	}

	internal Employee UpdateEmployeeStatus(
		EmployeeStatusType employeeStatusType,
		string changeReason,
		DateOnly? statusEffectiveDate,
		DateTimeOffset statusChangedAt)
	{
		EmployeeStatusType = employeeStatusType;
		ChangeReason = changeReason;
		StatusLastChangedAt = statusChangedAt;
		StatusEffectiveDate = employeeStatusType == Common.EmployeeStatusType.Active ? null : statusEffectiveDate;
		return this;
	}

	internal void AddDocument(EmployeeDocument document) => Documents.Add(document);
	internal void UpdateProfilePicture(string? profilePictureBlobName) => ProfilePictureBlobName = profilePictureBlobName;
	internal void UpdateUserId(Guid id) => UserId = id;
	public void AssignReportingManager(Guid reportingManagerId) => ReportingManagerId = reportingManagerId;
	public void RemoveReportingManager() => ReportingManagerId = null;

	private static string JoinNameParts(params string?[] parts) =>
		string.Join(" ", parts.Where(x => !string.IsNullOrWhiteSpace(x)));

	private static State? Clone(State? state) =>
		state == null ? null : new State(state.Name, state.IsoCode);

	private static Country? Clone(Country? country) =>
		country == null ? null : new Country(country.Name, country.IsoCode);
}
