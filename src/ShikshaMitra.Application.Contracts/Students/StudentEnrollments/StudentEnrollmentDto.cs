using ShikshaMitra.Common;
using ShikshaMitra.Students.StudentEnrollments.Registration;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using ShikshaMitra.TenantSettings.AcademicSetups.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments;

public class StudentEnrollmentDto : EntityDto<Guid>

{
	public StudentEnrollmentState StudentEnrollmentState { get; set; }
	// Inquiry Information
	public string InquiryNumber { get; set; }
	public string? FormNumber { get; set; }
	public string? FirstName { get; set; }
	public string? MiddleName { get; set; }
	public string? LastName { get; set; }
	public string? PreviousSchoolName { get; set; }
	public DateTimeOffset? DateOfBirth { get; set; }
	public DateTimeOffset DateOfInquiry { get; set; }
	public GenderType GenderType { get; set; }
	public string? ParentContactNumber { get; set; }
	public string? AlternativeContactNumber { get; set; }
	public string? AddressLine1 { get; set; }
	public string? AddressLine2 { get; set; }
	public string? AddressLine3 { get; set; }
	public string? Country { get; set; }
	public string? City { get; set; }
	public string? State { get; set; }
	public string? PinCode { get; set; }
	public InquiryStatusType? InquiryStatusType { get; set; }

	// Foreign Key 
	public Guid EducationMediumId { get;  set; }
	public EducationMediumDto EducationMedium { get; set; }
	public Guid EducationBoardId { get;  set; }
	public EducationBoardDto EducationBoard { get; set; }
	public Guid AcademicYearId { get;  set; }
	public AcademicYearDto AcademicYear { get; set; }
	public Guid StandardId { get;  set; }
	public StandardDto Standard { get; set; }
	public Guid? InquiryPreferenceId { get;  set; }
	public InquiryPreferenceDto InquiryPreference { get; set; }
	
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