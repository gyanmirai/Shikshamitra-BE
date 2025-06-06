using ShikshaMitra.Common;
using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments.Registration;

public class CreateStudentInquiryDto
{

	public string? FormNumber { get; set; }
	public string? FirstName { get; set; }
	public string? MiddleName { get; set; }
	public string? LastName { get; set; }
	public string? Email { get; set; }

	public string? PreviousSchoolName { get; set; }
	public DateTimeOffset? DateOfBirth { get; set; }
	public DateTimeOffset? DateOfInquiry { get; set; }
	public GenderType? GenderType { get; set; }
	public string? PrimaryContactNumber { get; set; }
	public string? SecondaryContactNumber { get; set; }
	public string? AddressLine1 { get; set; }
	public string? AddressLine2 { get; set; }
	public string? AddressLine3 { get; set; }
	public Country? Country { get; set; }
	public string? PAN { get; set; }
	public string? ApparId { get; set; }
	public string? City { get; set; }
	public State? State { get; set; }
	public string? PinCode { get; set; }

	public FollowUpStatusType InquiryStatusType { get; set; }

	// Foreign Key 
	public Guid AcademicYearId { get; set; }
	public Guid EducationBoardId { get; set; }
	public Guid EducationMediumId { get; set; }
	public Guid StandardId { get; set; }
	public Guid? InquiryPreferenceId { get; set; }
	
}