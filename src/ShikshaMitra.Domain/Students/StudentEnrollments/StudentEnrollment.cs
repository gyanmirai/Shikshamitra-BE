using ShikshaMitra.Common;
using ShikshaMitra.Students.StudentEnrollments.FollowUps;
using ShikshaMitra.Students.StudentEnrollments.Registration;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
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
	#region Properties

	public Guid? TenantId { get; private set; }

	// Inquiry Payload
	public string InquiryNumber { get; private set; }
	public string? FormNumber { get; private set; }
	public string? FirstName { get; private set; }
	public string? MiddleName { get; private set; }
	public string? LastName { get; private set; }
	public string? PreviousSchoolName { get; private set; }
	public DateTimeOffset? DateOfBirth { get; private set; }
	public DateTimeOffset? DateOfInquiry { get; private set; }
	public GenderType? GenderType { get; private set; }
	public string? PrimaryContactNumber { get; private set; }
	public string? SecondaryContactNumber { get; private set; }
	public string? AddressLine1 { get; private set; }
	public string? AddressLine2 { get; private set; }
	public string? AddressLine3 { get; private set; }
	public Country? Country { get; private set; }
	public State? State { get; private set; }

	public string? PAN { get; private set; }
	public string? ApparId { get; private set; }

	public string? City { get; private set; }
	public string? PinCode { get; private set; }
	public DateTimeOffset? DueDate { get; private set; }
	public string? Email { get; private set; }

	public float TotalAmount { get; private set; }
	public float PaidAmount { get; private set; }

	public StudentEnrollmentState StudentEnrollmentState { get; private set; }
	public StudentEnrollmentApprovalState StudentEnrollmentApprovalState { get; private set; }
	public FollowUpStatusType? FollowUpStatusType { get; private set; }
	public FeeStatusType FeeStatusType { get; private set; }
	public StudentEnrollmentInquiryState StudentEnrollmentInquiryState { get; private set; } = StudentEnrollmentInquiryState.New;

	public OnboardingProgressState OnboardingProgressState { get; set; } = OnboardingProgressState.General;

	public virtual ICollection<FollowUpMessage> FollowUps { get; private set; } = new List<FollowUpMessage>();

	// Foreign Keys
	public Guid AcademicYearId { get; private set; }
	public Guid EducationBoardId { get; private set; }
	public Guid EducationMediumId { get; private set; }
	public Guid StandardId { get; private set; }
	public Guid? InquiryPreferenceId { get; private set; }

	// Navigation Properties
	public AcademicYear AcademicYear { get; private set; }
	public EducationBoard EducationBoard { get; private set; }
	public EducationMedium EducationMedium { get; private set; }
	public Standard Standard { get; private set; }
	public InquiryPreference InquiryPreference { get; private set; }

	public Division Division { get; private set; }

	// Documents Value Object
	public List<StudentDocument> Documents { get; private set; } = new List<StudentDocument>();

	// Student Basic Details
	public Guid? UserId { get; private set; }
	public byte[]? ProfilePicture { get; private set; }
	public string? AadhaarNo { get; private set; }
	public string? Religion { get; private set; }
	public string? FamilyCast { get; private set; }
	public CastType? CastType { get; private set; }
	public string? MotherTongue { get; private set; }
	public string? Nationality { get; private set; }
	public string? BirthPlace { get; private set; }
	public string? Remarks { get; private set; }
	public Guid? DivisionId { get; set; }

	// Registration Details
	public StudentContactDetails FatherDetails { get; private set; }
	public StudentContactDetails MotherDetails { get; private set; }
	public StudentContactDetails GuardianDetails { get; private set; }
	public StudentCommunicationDetails StudentCommunicationDetails { get; private set; }
	public StudentAdditionalDetails StudentAdditionalDetails { get; private set; }
	public StudentMedicalRecords StudentMedicalRecords { get; private set; }
	public PreviousSchoolDetails PreviousSchoolDetails { get; private set; }
	public ParentBankDetails ParentBankDetails { get; private set; }

	#endregion

	#region Constructors

	private StudentEnrollment() { } // For ORM

	internal StudentEnrollment(
		Guid id,
		Guid? tenantId,
		StudentEnrollmentState studentEnrollmentState,
		StudentEnrollmentApprovalState studentEnrollmentApprovalState,
		string inquiryNumber,
		string? formNumber,
		string? firstName,
		string? middleName,
		string? lastName,
		string? previousSchoolName,
		DateTimeOffset? dateOfBirth,
		DateTimeOffset? dateOfInquiry,
		GenderType? genderType,
		string? primaryContactNumber,
		string? secondaryContactNumber,
		string? addressLine1,
		string? addressLine2,
		string? addressLine3,
		Country? country,
		State? state,
		string? pan,
		string? apparId,
		string? city,
		string? pinCode,
		FollowUpStatusType? inquiryStatusType,
		Guid academicYearId,
		Guid educationBoardId,
		Guid educationMediumId,
		Guid standardId,
		Guid? inquiryPreferenceId,
		// Basic Details
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
		string? remarks,
		// Registration Details
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
		StudentEnrollmentApprovalState = studentEnrollmentApprovalState;
		InquiryNumber = inquiryNumber;
		FormNumber = formNumber;
		FirstName = firstName;
		MiddleName = middleName;
		LastName = lastName;
		PreviousSchoolName = previousSchoolName;
		DateOfBirth = dateOfBirth;
		DateOfInquiry = dateOfInquiry;
		GenderType = genderType;
		PrimaryContactNumber = primaryContactNumber;
		SecondaryContactNumber = secondaryContactNumber;
		AddressLine1 = addressLine1;
		AddressLine2 = addressLine2;
		AddressLine3 = addressLine3;
		Country = country;
		PAN = pan;
		ApparId = apparId;
		City = city;
		State = state;
		PinCode = pinCode;
		FollowUpStatusType = inquiryStatusType;
		AcademicYearId = academicYearId;
		EducationBoardId = educationBoardId;
		EducationMediumId = educationMediumId;
		StandardId = standardId;
		InquiryPreferenceId = inquiryPreferenceId;

		// Basic Details
		UserId = userId;
		ProfilePicture = profilePicture;
		Email = email;
		AadhaarNo = aadhaarNo;
		Religion = religion;
		FamilyCast = familyCast;
		CastType = castType;
		MotherTongue = motherTongue;
		Nationality = nationality;
		BirthPlace = birthPlace;
		Remarks = remarks;

		// Registration Details
		FatherDetails = fatherDetails;
		MotherDetails = motherDetails;
		GuardianDetails = guardianDetails;
		StudentCommunicationDetails = studentCommunicationDetails;
		StudentAdditionalDetails = studentAdditionalDetails;
		StudentMedicalRecords = studentMedicalRecords;
		PreviousSchoolDetails = previousSchoolDetails;
		ParentBankDetails = parentBankDetails;
		OnboardingProgressState = OnboardingProgressState.General;
	}

	#endregion

	#region Update Methods

	internal StudentEnrollment UpdateStudentInquiry(
		StudentEnrollmentState studentEnrollmentState,
		StudentEnrollmentApprovalState studentEnrollmentApprovalState,
		string? formNumber,
		string? firstName,
		string? middleName,
		string? lastName,
		string? previousSchoolName,
		DateTimeOffset? dateOfBirth,
		DateTimeOffset? dateOfInquiry,
		GenderType? genderType,
		string? primaryContactNumber,
		string? secondaryContactNumber,
		string? addressLine1,
		string? addressLine2,
		string? addressLine3,
		Country? country,
		State? state,
		string? pan,
		string? apparId,
		string? city,
		string? pinCode,
		FollowUpStatusType? inquiryStatusType,
		Guid academicYearId,
		Guid educationBoardId,
		Guid educationMediumId,
		Guid standardId,
		Guid? inquiryPreferenceId
	)
	{
		StudentEnrollmentState = studentEnrollmentState;
		StudentEnrollmentApprovalState = studentEnrollmentApprovalState;
		FormNumber = formNumber;
		FirstName = firstName;
		MiddleName = middleName;
		LastName = lastName;
		PreviousSchoolName = previousSchoolName;
		DateOfBirth = dateOfBirth;
		DateOfInquiry = dateOfInquiry;
		GenderType = genderType;
		PrimaryContactNumber = primaryContactNumber;
		SecondaryContactNumber = secondaryContactNumber;
		AddressLine1 = addressLine1;
		AddressLine2 = addressLine2;
		AddressLine3 = addressLine3;
		Country = country;
		PAN = pan;
		ApparId = apparId;
		City = city;
		State = state;
		PinCode = pinCode;
		FollowUpStatusType = inquiryStatusType;
		AcademicYearId = academicYearId;
		EducationBoardId = educationBoardId;
		EducationMediumId = educationMediumId;
		StandardId = standardId;
		InquiryPreferenceId = inquiryPreferenceId;

		FollowUps = new List<FollowUpMessage>();
		return this;
	}

	internal StudentEnrollment UpdateStudentBasicDetails(
		string? formNumber,
		string? firstName,
		string? middleName,
		string? lastName,
		string? previousSchoolName,
		DateTimeOffset? dateOfBirth,
		GenderType? genderType,
		string? primaryContactNumber,
		string? secondaryContactNumber,
		string? addressLine1,
		string? addressLine2,
		string? addressLine3,
		Country? country,
		string? pan,
		string? apparId,
		string? city,
		State? state,
		string? pinCode,
		Guid academicYearId,
		Guid educationBoardId,
		Guid educationMediumId,
		Guid standardId,
		Guid? inquiryPreferenceId,
		// Basic Details
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
		PrimaryContactNumber = primaryContactNumber;
		SecondaryContactNumber = secondaryContactNumber;
		AddressLine1 = addressLine1;
		AddressLine2 = addressLine2;
		AddressLine3 = addressLine3;
		Country = country;
		PAN = pan;
		ApparId = apparId;
		City = city;
		State = state;
		PinCode = pinCode;
		AcademicYearId = academicYearId;
		EducationBoardId = educationBoardId;
		EducationMediumId = educationMediumId;
		StandardId = standardId;
		InquiryPreferenceId = inquiryPreferenceId;

		// Basic Details
		UserId = userId;
		ProfilePicture = profilePicture;
		Email = email;
		AadhaarNo = aadhaarNo;
		Religion = religion;
		FamilyCast = familyCast;
		CastType = castType;
		MotherTongue = motherTongue;
		Nationality = nationality;
		BirthPlace = birthPlace;
		Remarks = remarks;

		return this;
	}

	internal StudentEnrollment UpdateFamilyDetails(
		StudentContactDetails fatherDetails,
		StudentContactDetails motherDetails,
		StudentContactDetails guardianDetails
	)
	{
		FatherDetails = fatherDetails;
		MotherDetails = motherDetails;
		GuardianDetails = guardianDetails;
		return this;
	}

	internal StudentEnrollment UpdateCommunicationDetails(
		StudentCommunicationDetails studentCommunicationDetails
	)
	{
		StudentCommunicationDetails = studentCommunicationDetails;
		return this;
	}

	internal StudentEnrollment UpdateAdditionalDetails(
		StudentAdditionalDetails studentAdditionalDetails
	)
	{
		StudentAdditionalDetails = studentAdditionalDetails;
		return this;
	}

	internal StudentEnrollment UpdateMedicalReports(
		StudentMedicalRecords studentMedicalRecords
	)
	{
		StudentMedicalRecords = studentMedicalRecords;
		return this;
	}

	internal StudentEnrollment UpdatePreviousSchoolDetails(
		PreviousSchoolDetails previousSchoolDetails
	)
	{
		PreviousSchoolDetails = previousSchoolDetails;
		return this;
	}

	internal StudentEnrollment UpdateParenBankDetails(
		ParentBankDetails parentBankDetails
	)
	{
		ParentBankDetails = parentBankDetails;
		return this;
	}

	internal StudentEnrollment UpdateStudentEnrollmentState(
		StudentEnrollmentState studentEnrollmentState
	)
	{
		StudentEnrollmentState = studentEnrollmentState;
		return this;
	}

	public StudentEnrollment AddFollowUpMessage(FollowUpMessage followUpMessage)
	{
		FollowUps.Add(followUpMessage);
		return this;
	}

	internal StudentEnrollment UpdateStudentEnrollmentApprovalState(
		StudentEnrollmentApprovalState approvalState
	)
	{
		StudentEnrollmentApprovalState = approvalState;
		return this;
	}

	internal StudentEnrollment UpdateStudentEnrollmentInquiryState(
		StudentEnrollmentInquiryState inquiryState
	)
	{
		StudentEnrollmentInquiryState = inquiryState;
		return this;
	}

	public StudentEnrollment ChnageDueDate(DateTimeOffset? dueDate)
	{
		DueDate = dueDate;
		return this;
	}

	public StudentEnrollment ChangeInquiryStatus(FollowUpStatusType inquiryStatusType)
	{
		FollowUpStatusType = inquiryStatusType;
		return this;
	}

	public StudentEnrollment ChangeOnboardingProgressState(OnboardingProgressState onboardingProgressState)
	{
		// user can not go back to previous state
		if (OnboardingProgressState < onboardingProgressState)
		{
			OnboardingProgressState = onboardingProgressState;
		}
		return this;
	}

	public void AddDocument(StudentDocument document)
	{
		Documents.Add(document);
	}

	#endregion
}