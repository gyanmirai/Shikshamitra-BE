using ShikshaMitra.Common;
using ShikshaMitra.Students.StudentEnrollments.Registration;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments;

public class StudentEnrollment : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	// public Guid UserId { get; private set; }
	// public Guid RoleId { get; private set; }

	public StudentEnrollmentState StudentEnrollmentState { get; private set; }

	// Inquiry and Registration Form
	public string InquiryNumber { get; private set; }
	public string RegistrationCode { get; private set; }
	public string? FormNumber { get; private set; }
	public string? FirstName { get; private set; }
	public string? MiddleName { get; private set; }
	public string? LastName { get; private set; }
	public string? PreviousSchoolName { get; private set; }
	public DateTimeOffset? DateOfBirth { get; private set; }
	public DateTimeOffset DateOfInquiry { get; private set; }
	public GenderType GenderType { get; private set; }
	public string? ParentContactNumber { get; private set; }
	public string? AlternativeContactNumber { get; private set; }
	public string? City { get; private set; }
	public string? State { get; private set; }
	public int? PinCode { get; private set; }
	public string? Address { get; private set; }
	public InquiryStatusType? InquiryStatusType { get; private set; }
	
	// Foreign Key 
	public Guid AcademicYearId { get; private set; }
	public Guid EducationBoardId { get; private set; }
	public Guid EducationMediumId { get; private set; }
	public Guid StandardId { get; private set; }
	public Guid InquiryPreferenceId { get; private set; }

	// Navigation Property for student enrollment
	public AcademicYear AcademicYear { get; private set; }
	public EducationBoard EducationBoard { get; private set; }
	public EducationMedium EducationMedium { get; private set; }
	public Standard Standard { get; private set; }	
	public InquiryPreference InquiryPreference { get; private set; }
	
	// Navigation Property for Role
	public virtual IdentityRole? Role { get; private set; }

	// For Registration
	public byte[]? ProfilePicture { get; private set; }
	public string? AadhaarNo { get; private set; }
	public string? Religion { get; private set; }
	public string? FamilyCast { get; private set; }
	public CastType? CastType { get; private set; }
	public string? MotherTongue { get; private set; }
	public string? Nationality { get; private set; }
	public string? BirthPlace { get; private set; }
	public string? TemporaryGr { get; private set; }
	public string? Remarks { get; private set; }
	
	// Value Object
	public List<StudentDocument> Documents { get; private set; } = new List<StudentDocument>();
	public StudentContactDetails FatherDetails { get; private set; }
	public StudentContactDetails MotherDetails { get; private set; }
	public StudentContactDetails GuardianDetails { get; private set; }
	public StudentCommunicationDetails StudentCommunicationDetails { get; private set; }
	public StudentAdditionalDetails StudentAdditionalDetails { get; private set; }
	public StudentMedicalRecords StudentMedicalRecords { get; private set; }
	public PreviousSchoolDetails PreviousSchoolDetails { get; private set; }
	public ParentBankDetails ParentBankDetails { get; private set; }

	private StudentEnrollment() {} // For ORM

	internal StudentEnrollment(
		Guid id,
		Guid? tenantId,
		// Guid userId,
		// Guid? roleId,
		StudentEnrollmentState studentEnrollmentState,
		string inquiryNumber,
		string registrationCode,
		string? formNumber,
		string? firstName,
		string? middleName,
		string? lastName,
		string? previousSchoolName,
		DateTimeOffset dateOfBirth,
		DateTimeOffset dateOfInquiry,
		GenderType genderType,
		string? parentContactNumber,
		string? alternativeContactNumber,
		string? city,
		string? state,
		int? pinCode,
		string? address,
		InquiryStatusType? inquiryStatusType,
		Guid academicYearId,
		Guid educationBoardId,
		Guid educationMediumId,
		Guid standardId,
		Guid inquiryPreferenceId,
		// registration
		byte[]? profilePicture,
		string? aadhaarNo,
		string? religion,
		string? familyCast,
		CastType? castType,
		string? motherTongue,
		string? nationality,
		string? birthPlace,
		string? temporaryGr,
		string? remarks,
		
		StudentContactDetails fatherDetails,
		StudentContactDetails motherDetails,
		StudentContactDetails guardianDetails,
		StudentCommunicationDetails studentCommunicationDetails,
		StudentAdditionalDetails studentAdditionalDetails,
		StudentMedicalRecords studentMedicalRecords,
		PreviousSchoolDetails previousSchoolDetails,
		ParentBankDetails parentBankDetails
	) : base(id)
	{
		TenantId = tenantId;
		// UserId = userId;
		// RoleId = Check.NotNull(roleId, nameof(roleId)) ?? throw new ArgumentNullException(nameof(roleId));;
		StudentEnrollmentState = studentEnrollmentState;
		InquiryNumber = inquiryNumber;
		RegistrationCode = registrationCode;
		FormNumber = formNumber;
		FirstName = firstName;
		MiddleName = middleName;
		LastName = lastName;
		PreviousSchoolName = previousSchoolName;
		DateOfBirth = dateOfBirth;
		DateOfInquiry = dateOfInquiry;
		GenderType = genderType;
		ParentContactNumber = parentContactNumber;
		AlternativeContactNumber = alternativeContactNumber;
		City = city;
		State = state;
		PinCode = pinCode;
		Address = address;
		InquiryStatusType = inquiryStatusType;
		AcademicYearId = academicYearId;
		EducationBoardId = educationBoardId;
		EducationMediumId = educationMediumId;
		StandardId = standardId;
		InquiryPreferenceId = inquiryPreferenceId;
		// enrollment
		ProfilePicture = profilePicture;
		AadhaarNo = aadhaarNo;
		Religion = religion;
		FamilyCast = familyCast;
		CastType = castType;
		MotherTongue = motherTongue;
		Nationality = nationality;
		BirthPlace = birthPlace;
		TemporaryGr = temporaryGr;
		Remarks = remarks;
		FatherDetails = fatherDetails;
		MotherDetails = motherDetails;
		GuardianDetails = guardianDetails;
		StudentCommunicationDetails = studentCommunicationDetails;
		StudentAdditionalDetails = studentAdditionalDetails;
		StudentMedicalRecords = studentMedicalRecords;
		PreviousSchoolDetails = previousSchoolDetails;
		ParentBankDetails = parentBankDetails;
	}

	internal StudentEnrollment UpdateGeneralInfo(
		Guid? roleId,
		StudentEnrollmentState studentEnrollmentState,
		string? formNumber,
		string? firstName,
		string? middleName,
		string? lastName,
		string? previousSchoolName,
		DateTimeOffset dateOfBirth,
		DateTimeOffset dateOfInquiry,
		GenderType genderType,
		string? parentContactNumber,
		string? alternativeContactNumber,
		string? city,
		string? state,
		int? pinCode,
		string? address,
		InquiryStatusType? inquiryStatusType,
		Guid academicYearId,
		Guid educationBoardId,
		Guid educationMediumId,
		Guid standardId,
		Guid inquiryPreferenceId,
		// registration
		byte[]? profilePicture,
		string? aadhaarNo,
		string? religion,
		string? familyCast,
		CastType? castType,
		string? motherTongue,
		string? nationality,
		string? birthPlace,
		string? temporaryGr,
		string? remarks
	)
	{
		// RoleId = Check.NotNull(roleId, nameof(roleId)) ?? throw new ArgumentNullException(nameof(roleId));
		StudentEnrollmentState = studentEnrollmentState;
		FormNumber = formNumber;
		FirstName = firstName;
		MiddleName = middleName;
		LastName = lastName;
		PreviousSchoolName = previousSchoolName;
		DateOfBirth = dateOfBirth;
		DateOfInquiry = dateOfInquiry;
		GenderType = genderType;
		ParentContactNumber = parentContactNumber;
		AlternativeContactNumber = alternativeContactNumber;
		City = city;
		State = state;
		PinCode = pinCode;
		Address = address;
		InquiryStatusType = inquiryStatusType;
		AcademicYearId = academicYearId;
		EducationBoardId = educationBoardId;
		EducationMediumId = educationMediumId;
		StandardId = standardId;
		InquiryPreferenceId = inquiryPreferenceId;
		ProfilePicture = profilePicture;
		AadhaarNo = aadhaarNo;
		Religion = religion;
		FamilyCast = familyCast;
		CastType = castType;
		MotherTongue = motherTongue;
		Nationality = nationality;
		BirthPlace = birthPlace;
		TemporaryGr = temporaryGr;
		Remarks = remarks;
		return this;
	}
	
	
	// +++++++++++ Update Family Details ++++++++++++
	internal StudentEnrollment UpdateFamilyDetails(
		StudentContactDetails fatherDetails,
		StudentContactDetails motherDetails,
		StudentContactDetails guardianDetails
	)
	{
		this.FatherDetails = fatherDetails;
		this.MotherDetails = motherDetails;
		this.GuardianDetails = guardianDetails;
		return this;
	}

	// ++++++++ Update Communication Details +++++++
	internal StudentEnrollment UpdateCommunicationDetails(
		StudentCommunicationDetails studentCommunicationDetails
	)
	{
		this.StudentCommunicationDetails = studentCommunicationDetails;
		return this;
	}
	

	// +++++++++ Update Additional Details ++++++++++++
	internal StudentEnrollment UpdateAdditionalDetails(
		StudentAdditionalDetails  studentAdditionalDetails
	)
	{
		this.StudentAdditionalDetails = studentAdditionalDetails;
		return this; 
	}

	// +++++++++ Update Medical Details +++++++
	internal StudentEnrollment UpdateMedicalReports(
		StudentMedicalRecords studentMedicalRecords
	)
	{
		this.StudentMedicalRecords = studentMedicalRecords;
		return this;
	}

	// +++++++++ Update Previous School Details +++++++
	internal StudentEnrollment UpdatePreviousSchoolDetails(
		PreviousSchoolDetails previousSchoolDetails
	)
	{
		this.PreviousSchoolDetails = previousSchoolDetails;
		return this;
	}

	// +++++++++ Update Parent Bank Details +++++++++
	internal StudentEnrollment UpdateParenBankDetails(
		ParentBankDetails parentBankDetails
	)
	{
		this.ParentBankDetails = parentBankDetails;
		return this;
	}

	// +++++++++ Add Student Documeent +++++++++
	internal void AddDocument(StudentDocument document) => Documents.Add(document);
}

