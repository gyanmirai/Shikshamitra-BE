using ShikshaMitra.Common;
using ShikshaMitra.Students.StudentEnrollments.Registration;
using System;

namespace ShikshaMitra.Students.StudentEnrollments;

public class CreateUpdateStudentEnrollmentDto
{
	public StudentEnrollmentState StudentEnrollmentState { get; set; }
	// Inquiry and Registration Form
	public string InquiryNumber { get; set; }
	public string? FormNumber { get; set; }
	public string? FirstName { get; set; }
	public string? MiddleName { get; set; }
	public string? LastName { get; set; }
	public string? PreviousSchoolName { get; set; }
	public DateTimeOffset? DateOfBirth { get; set; }
	public DateTimeOffset? DateOfInquiry { get; set; }
	public GenderType? GenderType { get; set; }
	public string? ParentContactNumber { get; set; }
	public string? AlternativeContactNumber { get; set; }
	public string? AddressLine1 {get; set; }
	public string? AddressLine2 {get; set; }
	public string? AddressLine3 {get; set; }
	public string? Country {get; set; }
	public string? City { get; set; }
	public string? State { get; set; }
	public string? PinCode { get; set; }
	public InquiryStatusType? InquiryStatusType { get; set; }

	// Foreign Key 
	public Guid AcademicYearId { get; set; }
	public Guid EducationBoardId { get; set; }
	public Guid EducationMediumId { get; set; }
	public Guid StandardId { get; set; }

	// Value Object
	public StudentBasicDetailsDto StudentBasicDetails { get; private set; }
	public StudentContactDetailsDto FatherDetails { get; private set; }
	public StudentContactDetailsDto MotherDetails { get; private set; }
	public StudentContactDetailsDto GuardianDetails { get; private set; }
	public StudentCommunicationDetailsDto StudentCommunicationDetails { get; private set; }
	public StudentAdditionalDetailsDto StudentAdditionalDetails { get; private set; }
	public StudentMedicalRecordsDto StudentMedicalRecords { get; private set; }
	public PreviousSchoolDetailsDto PreviousSchoolDetails { get; private set; }
	public ParentBankDetailsDto ParentBankDetails { get; private set; }
}