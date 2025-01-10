using ShikshaMitra.Common;
using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments.Registration;

public class CreateStudentEnrollmentBasicInfoDto
{
	// public Guid UserId { get; set; }
	// [Required(ErrorMessage = "Role is required.")]
	// public Guid RoleId { get; set; }
	public StudentEnrollmentState StudentEnrollmentState { get; set; }

	// Inquiry and Registration Form
	public string InquiryNumber { get; set; }
	public string RegistrationCode { get; set; }
	public string? FormNumber { get; set; }
	public string? FirstName { get; set; }
	public string? MiddleName { get; set; }
	public string? LastName { get; set; }
	public string? PreviousSchoolName { get; set; }
	public DateTimeOffset DateOfBirth { get; set; }
	public DateTimeOffset DateOfInquiry { get; set; }
	public GenderType GenderType { get; set; }
	public string? ParentContactNumber { get; set; }
	public string? AlternativeContactNumber { get; set; }
	public string? City { get; set; }
	public string? State { get; set; }
	public int? PinCode { get; set; }
	public string? Address { get; set; }
	
	public string InquiryStatusType { get; set; }

	// Foreign Key 
	public Guid AcademicYearId { get; set; }
	public Guid EducationBoardId { get; set; }
	public Guid EducationMediumId { get; set; }
	public Guid StandardId { get; set; }
	public Guid InquiryPreferenceId { get; set; }

	// For Registration
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
}