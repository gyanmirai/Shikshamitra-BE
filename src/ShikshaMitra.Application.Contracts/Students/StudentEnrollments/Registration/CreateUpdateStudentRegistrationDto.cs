using ShikshaMitra.Common;
using System;

namespace ShikshaMitra.Students.StudentEnrollments.Registration;

public class CreateUpdateStudentRegistrationDto
{
	public string? FormNumber { get; set; }
	public string? FirstName { get; set; }
	public string? MiddleName { get; set; }
	public string? LastName { get; set; }
	public string? PreviousSchoolName { get; set; }
	public DateTimeOffset? DateOfBirth { get; set; }
	public GenderType GenderType { get; set; }
	public string? ParentContactNumber { get; set; }
	public string? AlternativeContactNumber { get; set; }
	public string? AddressLine1 {get; set; }
	public string? AddressLine2 {get; set; }
	public string? AddressLine3 {get; set; }
	public Country? Country {get; set; }
	public string? Village { get; set; }
	public string? PAN { get; set; }
	public string? ApparId { get; set; }
	public string? City { get; set; }
	public State? State { get; set; }
	public string? PinCode { get; set; }

	// Foreign Key 
	public Guid AcademicYearId { get; set; }
	public Guid EducationBoardId { get; set; }
	public Guid EducationMediumId { get; set; }
	public Guid StandardId { get; set; }
	public Guid? InquiryPreferenceId { get; set; }

	// basic details
	public Guid? UserId { get; set; }
	public byte[]? ProfilePicture { get; set; }
	public string? Email { get; set; }
	public string? AadhaarNo { get; set; }
	public string? Religion { get; set; }
	public string? FamilyCast { get; set; }
	public CastType? CastType { get; set; }
	public string? MotherTongue { get; set; }
	public string? Nationality { get; set; }
	public string? BirthPlace { get; set; }
	public int TemporaryGr { get; set; }
	public string? Remarks { get; set; }
}