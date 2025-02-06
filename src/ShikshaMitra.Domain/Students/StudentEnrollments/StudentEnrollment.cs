using ShikshaMitra.Common;
using ShikshaMitra.Students.StudentEnrollments.FollowUps;
using ShikshaMitra.Students.StudentEnrollments.Registration;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments;

public class StudentEnrollment : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public StudentEnrollmentState StudentEnrollmentState { get; private set; }
	// Student Inquiry payload
	public string InquiryNumber { get; private set; }
	public string? FormNumber { get; private set; }
	public string? FirstName { get; private set; }
	public string? MiddleName { get; private set; }
	public string? LastName { get; private set; }
	public string? PreviousSchoolName { get; private set; }
	public DateTimeOffset? DateOfBirth { get; private set; }
	public DateTimeOffset? DateOfInquiry { get; private set; }
	public GenderType? GenderType { get; private set; }
	public string? ParentContactNumber { get; private set; }
	public string? AlternativeContactNumber { get; private set; }
	public string? AddressLine1 { get; private set; }
	public string? AddressLine2 { get; private set; }
	public string? AddressLine3 { get; private set; }
	public string? Country { get; private set; }
	public string? City { get; private set; }
	public string? State { get; private set; }
	public string? PinCode { get; private set; }
	public InquiryStatusType? InquiryStatusType { get; private set; }

	public DateTimeOffset? DueDate { get; private set; }
	public virtual ICollection<FollowUpMessage> FollowUps { get; private set; }


	// Foreign Key 
	public Guid AcademicYearId { get; private set; }
	public Guid EducationBoardId { get; private set; }
	public Guid EducationMediumId { get; private set; }
	public Guid StandardId { get; private set; }
	public Guid? InquiryPreferenceId { get; private set; }


	// Navigation Property for student enrollment
	public AcademicYear AcademicYear { get; private set; }
	public EducationBoard EducationBoard { get; private set; }
	public EducationMedium EducationMedium { get; private set; }
	public Standard Standard { get; private set; }
	public InquiryPreference InquiryPreference { get; private set; }
	// Value Object
	public List<StudentDocument> Documents { get; private set; } = new List<StudentDocument>();
	public StudentBasicDetails StudentBasicDetails { get; private set; }
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
		StudentEnrollmentState studentEnrollmentState,
		string inquiryNumber,
		string? formNumber,
		string? firstName,
		string? middleName,
		string? lastName,
		string? previousSchoolName,
		DateTimeOffset? dateOfBirth,
		DateTimeOffset? dateOfInquiry,
		GenderType? genderType,
		string? parentContactNumber,
		string? alternativeContactNumber,
		string? addressLine1,
		string? addressLine2,
		string? addressLine3,
		string? country,
		string? state,
		string? city,
		string? pinCode,
		InquiryStatusType? inquiryStatusType,
		Guid academicYearId,
		Guid educationBoardId,
		Guid educationMediumId,
		Guid standardId,
		Guid? inquiryPreferenceId,
		// registration
		StudentBasicDetails studentBasicDetails,
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
		StudentEnrollmentState = studentEnrollmentState;
		InquiryNumber = inquiryNumber;
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
		AddressLine1 = addressLine1;
		AddressLine2 = addressLine2;
		AddressLine3 = addressLine3;
		Country = country;
		City = city;
		State = state;
		PinCode = pinCode;
		InquiryStatusType = inquiryStatusType;
		AcademicYearId = academicYearId;
		EducationBoardId = educationBoardId;
		EducationMediumId = educationMediumId;
		StandardId = standardId;
		InquiryPreferenceId = inquiryPreferenceId;
		// registration
		StudentBasicDetails = studentBasicDetails;
		FatherDetails = fatherDetails;
		MotherDetails = motherDetails;
		GuardianDetails = guardianDetails;
		StudentCommunicationDetails = studentCommunicationDetails;
		StudentAdditionalDetails = studentAdditionalDetails;
		StudentMedicalRecords = studentMedicalRecords;
		PreviousSchoolDetails = previousSchoolDetails;
		ParentBankDetails = parentBankDetails;
	}

	internal StudentEnrollment UpdateStudentInquiry(
		StudentEnrollmentState studentEnrollmentState,
		string? formNumber,
		string? firstName,
		string? middleName,
		string? lastName,
		string? previousSchoolName,
		DateTimeOffset? dateOfBirth,
		DateTimeOffset? dateOfInquiry,
		GenderType? genderType,
		string? parentContactNumber,
		string? alternativeContactNumber,
		string? addressLine1,
		string? addressLine2,
		string? addressLine3,
		string? country,
		string? state,
		string? city,
		string? pinCode,
		InquiryStatusType? inquiryStatusType,
		Guid academicYearId,
		Guid educationBoardId,
		Guid educationMediumId,
		Guid standardId,
		Guid? inquiryPreferenceId
	)
	{
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
		AddressLine1 = addressLine1;
		AddressLine2 = addressLine2;
		AddressLine3 = addressLine3;
		Country = country;
		City = city;
		State = state;
		PinCode = pinCode;
		InquiryStatusType = inquiryStatusType;
		AcademicYearId = academicYearId;
		EducationBoardId = educationBoardId;
		EducationMediumId = educationMediumId;
		StandardId = standardId;
		InquiryPreferenceId = inquiryPreferenceId;
        FollowUps = new List<FollowUpMessage>();
        return this;
	}
	
	// ++++++++ Update Student Basic Details +++++++
	internal StudentEnrollment UpdateStudentBasicDetails(
		string? formNumber,
		string? firstName,
		string? middleName,
		string? lastName,
		string? previousSchoolName,
		DateTimeOffset? dateOfBirth,
		GenderType? genderType,
		string? parentContactNumber,
		string? alternativeContactNumber,
		string? addressLine1,
		string? addressLine2,
		string? addressLine3,
		string? country,
		string? city,
		string? state,
		string? pinCode,
		Guid academicYearId,
		Guid educationBoardId,
		Guid educationMediumId,
		Guid standardId,
		Guid? inquiryPreferenceId,
		// basic details
		Guid? userId,
		byte[]? profilePicture,
		string? email,
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
		FormNumber = formNumber;
		FirstName = firstName;
		MiddleName = middleName;
		LastName = lastName;
		PreviousSchoolName = previousSchoolName;
		DateOfBirth = dateOfBirth;
		GenderType = genderType;
		ParentContactNumber = parentContactNumber;
		AlternativeContactNumber = alternativeContactNumber;
		AddressLine1 = addressLine1;
		AddressLine2 = addressLine2;
		AddressLine3 = addressLine3;
		Country = country;
		City = city;
		State = state;
		PinCode = pinCode;
		AcademicYearId = academicYearId;
		EducationBoardId = educationBoardId;
		EducationMediumId = educationMediumId;
		StandardId = standardId;
		InquiryPreferenceId = inquiryPreferenceId;
		StudentBasicDetails = new StudentBasicDetails(
			userId: userId,
			profilePicture: profilePicture,
			email: email,
			aadhaarNo: aadhaarNo,
			religion: religion,
			familyCast: familyCast,
			castType: castType,
			motherTongue: motherTongue,
			nationality: nationality,
			birthPlace: birthPlace,
			temporaryGr: temporaryGr,
			remarks: remarks
		);
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

	// +++++++++ Update Student Enrollment State to registration +++++++++
	internal StudentEnrollment UpdateStudentEnrollmentState(StudentEnrollmentState studentEnrollmentState)
	{
		StudentEnrollmentState = studentEnrollmentState;
		return this;
	}

	// +++++++++ Add Student Documeent +++++++++
	internal void AddDocument(StudentDocument document) => Documents.Add(document);


	public StudentEnrollment AddFollowUpMessage(FollowUpMessage followUpMessage)
	{
		FollowUps.Add(followUpMessage);
		return this;
	}


	public StudentEnrollment ChnageDueDate(DateTimeOffset? dueDate)
	{
		DueDate = dueDate;
		return this;
	}

	public StudentEnrollment ChangeInquiryStatus(InquiryStatusType inquiryStatusType)
	{
		InquiryStatusType = inquiryStatusType;
		return this;
	}
}