using ShikshaMitra.Common;
using ShikshaMitra.Students.Compliance;
using System;

namespace ShikshaMitra.Students.Enrollments.Registrations;

public class StudentGeneralDetailsDto
{
	public Guid StudentId { get; set; }
	public string FormNo { get; set; }
	public Guid AcademicYearId { get; set; }
	public Guid AcademicBoardId { get; set; }
	public Guid AcademicMediumId { get; set; }
	public Guid StandardId { get; set; }

	public string FirstName { get; set; }
	public string? MiddleName { get; set; }
	public string LastName { get; set; }
	public DateTimeOffset DateOfBirth { get; set; }
	public GenderEnum Gender { get; set; }

	public string PrimaryContactNo { get; set; }
	public string? SecondaryContactNo { get; set; }
	public string? Email { get; set; }

	public string? AdharNo { get; set; }
	public string? AadhaarMasked { get; set; }
	public IdentityVerificationStatus AadhaarVerificationStatus { get; set; } = IdentityVerificationStatus.NotProvided;
	public string? ApparId { get; set; }
	public ConsentStatus ApaarConsentStatus { get; set; } = ConsentStatus.NotAsked;
	public IdentityVerificationStatus ApaarVerificationStatus { get; set; } = IdentityVerificationStatus.NotProvided;
	public string? PanNo { get; set; }
	public string? PennNo { get; set; }
	public string? UdiseStudentCode { get; set; }
	public UdiseSyncStatus UdiseSyncStatus { get; set; } = UdiseSyncStatus.NotReady;
	public bool? RteStatus { get; set; }
	public ScholarshipStatus ScholarshipStatus { get; set; } = ScholarshipStatus.NotApplicable;
	public Guid? InquiryPreferenceId { get; set; }
	public ReligionEnum? Religion { get; set; }
	public string FamilyCast { get; set; }
	public CastCategoryEnum? CastCategory { get; set; }
	public string? Nationality { get; set; }
	public string? BirthPlace { get; set; }
	public MotherTongueEnum? MotherTongue { get; set; }

	public string Address1 { get; set; }
	public string Address2 { get; set; }
	public string? Address3 { get; set; }
	public Country Country { get; set; }
	public State State { get; set; }
	public string City { get; set; }
	public string? Pincode { get; set; }

	public string? PreviousSchool { get; set; }
	public string? LastClassPassed { get; set; }
}
