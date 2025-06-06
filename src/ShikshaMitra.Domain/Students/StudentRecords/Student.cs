using ShikshaMitra.Common;
using ShikshaMitra.Students.StudentEnrollments.Registration;
using ShikshaMitra.TenantSettings.AcademicSetups.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentRecords;


public class Student : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	#region Basic Properties
	public Guid? TenantId { get; private set; }
	public string? FormNumber { get; private set; }
	public string? FirstName { get; private set; }
	public string? MiddleName { get; private set; }
	public string? LastName { get; private set; }
	public DateTimeOffset? DateOfBirth { get; private set; }
	public GenderType? GenderType { get; private set; }
	public string? Email { get; private set; }
	public string? ParentContactNumber { get; private set; }
	public string? AlternativeContactNumber { get; private set; }
	public byte[]? ProfilePicture { get; private set; }
	public StudentStatus Status { get; private set; }
	#endregion

	#region Address Details
	public string? AddressLine1 { get; private set; }
	public string? AddressLine2 { get; private set; }
	public string? AddressLine3 { get; private set; }
	public Country? Country { get; private set; }
	public State? State { get; private set; }
	public string? City { get; private set; }
	public string? PinCode { get; private set; }
	#endregion

	#region Personal Details
	public string? AadhaarNo { get; private set; }
	public string? Religion { get; private set; }
	public string? FamilyCast { get; private set; }
	public CastType? CastType { get; private set; }
	public string? MotherTongue { get; private set; }
	public string? Nationality { get; private set; }
	public string? BirthPlace { get; private set; }
	public string? Remarks { get; private set; }
	public Guid? UserId { get; private set; }
	#endregion

	#region Academic Details

	public int? GrNoRecord { get; set; }
	public int? TempGrNo { get; private set; }
	public int? RollNo { get; set; }
	public Guid AcademicYearId { get; private set; }
	public Guid EducationBoardId { get; private set; }
	public Guid EducationMediumId { get; private set; }
	public Guid? EducationMediumStageId { get; private set; }
	public Guid? StandardId { get;  set; }
	public Guid? DivisionId { get; set; }
	#endregion

	#region Navigation Properties
	public virtual AcademicYear AcademicYear { get; private set; }
	public virtual EducationBoard EducationBoard { get; private set; }
	public virtual EducationMedium EducationMedium { get; private set; }
	public virtual EducationMediumStage? EducationMediumStage { get; private set; }
	public virtual Standard? Standard { get; private set; }
	public virtual Division? Division { get; private set; }
	#endregion

	#region Family Details
	public StudentContactDetails FatherDetails { get; private set; }
	public StudentContactDetails MotherDetails { get; private set; }
	public StudentContactDetails GuardianDetails { get; private set; }
	#endregion

	#region Additional Details
	public StudentCommunicationDetails StudentCommunicationDetails { get; private set; }
	public StudentAdditionalDetails StudentAdditionalDetails { get; private set; }
	public StudentMedicalRecords StudentMedicalRecords { get; private set; }
	public PreviousSchoolDetails PreviousSchoolDetails { get; private set; }
	public ParentBankDetails ParentBankDetails { get; private set; }
	public List<StudentDocument> Documents { get; private set; } = new List<StudentDocument>();
	#endregion

	protected Student() { }

	public Student(
		Guid id,
		Guid? tenantId,
		string? formNumber,
		string? firstName,
		string? middleName,
		string? lastName,
		DateTimeOffset? dateOfBirth,
		GenderType? genderType,
		string? email,
		string? parentContactNumber,
		string? alternativeContactNumber,
		byte[]? profilePicture,

		// Address
		string? addressLine1,
		string? addressLine2,
		string? addressLine3,
		Country? country,
		State? state,
		string? city,
		string? pinCode,

		// Personal Details
		string? aadhaarNo,
		string? religion,
		string? familyCast,
		CastType? castType,
		string? motherTongue,
		string? nationality,
		string? birthPlace,
		string? remarks,
		Guid? userId,

		// Academic Details
		Guid academicYearId,
		Guid educationBoardId,
		Guid educationMediumId,
		Guid? educationMediumStageId,
		Guid standardId,
		Guid? divisionId,
		int? grNoRecord,
		int? rollNo,

		// Family Details
		StudentContactDetails fatherDetails,
		StudentContactDetails motherDetails,
		StudentContactDetails guardianDetails,

		// Additional Details
		StudentCommunicationDetails studentCommunicationDetails,
		StudentAdditionalDetails studentAdditionalDetails,
		StudentMedicalRecords studentMedicalRecords,
		PreviousSchoolDetails previousSchoolDetails,
		ParentBankDetails parentBankDetails,
		List<StudentDocument> documents = null
	) : base(id)
	{
		TenantId = tenantId;
		FormNumber = formNumber;
		FirstName = firstName;
		MiddleName = middleName;
		LastName = lastName;
		DateOfBirth = dateOfBirth;
		GenderType = genderType;
		Email = email;
		ParentContactNumber = parentContactNumber;
		AlternativeContactNumber = alternativeContactNumber;
		ProfilePicture = profilePicture;

		// Address
		AddressLine1 = addressLine1;
		AddressLine2 = addressLine2;
		AddressLine3 = addressLine3;
		Country = country;
		State = state;
		City = city;
		PinCode = pinCode;

		// Personal Details
		AadhaarNo = aadhaarNo;
		Religion = religion;
		FamilyCast = familyCast;
		CastType = castType;
		MotherTongue = motherTongue;
		Nationality = nationality;
		BirthPlace = birthPlace;
		Remarks = remarks;
		UserId = userId;

		// Academic Details
		AcademicYearId = academicYearId;
		EducationBoardId = educationBoardId;
		EducationMediumId = educationMediumId;
		EducationMediumStageId = educationMediumStageId;
		StandardId = standardId;
		DivisionId = divisionId;
		GrNoRecord = grNoRecord;
		RollNo = rollNo;

		// Family Details
		FatherDetails = fatherDetails;
		MotherDetails = motherDetails;
		GuardianDetails = guardianDetails;

		// Additional Details
		StudentCommunicationDetails = studentCommunicationDetails;
		StudentAdditionalDetails = studentAdditionalDetails;
		StudentMedicalRecords = studentMedicalRecords;
		PreviousSchoolDetails = previousSchoolDetails;
		ParentBankDetails = parentBankDetails;
		Documents = documents ?? new List<StudentDocument>();
		Status = StudentStatus.Active;
	}

	public void UpdateStatus(StudentStatus newStatus)
	{
		Status = newStatus;
	}
}