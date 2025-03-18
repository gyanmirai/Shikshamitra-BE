using ShikshaMitra.Common;
using ShikshaMitra.Students.FollowUps;
using ShikshaMitra.Students.StudentEnrollments.Registration;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using ShikshaMitra.TenantSettings.AcademicSetups.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments;

public class StudentEnrollmentDto : EntityDto<Guid>
{
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
	public Country? Country { get; set; }
	public string? City { get; set; }
	public State? State { get; set; }
	public string? PinCode { get; set; }
	public string? Email { get; set; }

	public StudentEnrollmentState StudentEnrollmentState { get; private set; }
	public StudentEnrollmentApprovalState StudentEnrollmentApprovalState { get; private set; }
	public FollowUpStatusType? FollowUpStatusType { get; private set; }
	public FeeStatusType FeeStatusType { get; private set; }
	public StudentEnrollmentInquiryState StudentEnrollmentInquiryState { get; private set; }

	// Foreign Key
	public Guid EducationMediumId { get; set; }
	public EducationMediumDto EducationMedium { get; set; }
	public Guid EducationBoardId { get; set; }
	public EducationBoardDto EducationBoard { get; set; }
	public Guid AcademicYearId { get; set; }
	public AcademicYearDto AcademicYear { get; set; }
	public Guid StandardId { get; set; }
	public StandardDto Standard { get; set; }
	public Guid? InquiryPreferenceId { get; set; }
	public InquiryPreferenceDto InquiryPreference { get; set; }

	public Guid? DivisionId { get; set; }
	public DivisionDto? Division { get; set; }

	public DateTimeOffset? DueDate { get; set; }
    public List<FollowUpMessageDto> FollowUps { get; set; } = new();

	//  StudentBasicDetails properties moved directly into StudentEnrollmentDto
	public Guid? UserId { get; set; }
	public byte[]? ProfilePicture { get; set; }
	public string? AadhaarNo { get; set; }
	public string? Religion { get; set; }
	public string? FamilyCast { get; set; }
	public CastType? CastType { get; set; }
	public string? MotherTongue { get; set; }
	public string? Nationality { get; set; }
	public string? BirthPlace { get; set; }
	public string? TemporaryGr { get; set; }
	public string? Remarks { get; set; }

	// Value Objects
	public StudentContactDetailsDto FatherDetails { get; set; }
	public StudentContactDetailsDto MotherDetails { get; set; }
	public StudentContactDetailsDto GuardianDetails { get; set; }
	public StudentCommunicationDetailsDto StudentCommunicationDetails { get; set; }
	public StudentAdditionalDetailsDto StudentAdditionalDetails { get; set; }
	public StudentMedicalRecordsDto StudentMedicalRecords { get; set; }
	public PreviousSchoolDetailsDto PreviousSchoolDetails { get; set; }
	public ParentBankDetailsDto ParentBankDetails { get; set; }
}