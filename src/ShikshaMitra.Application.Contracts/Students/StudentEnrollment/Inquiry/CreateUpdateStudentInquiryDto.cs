using ShikshaMitra.Common;
using System;

namespace ShikshaMitra.Students.StudentEnrollment.Inquiry;

public class CreateUpdateStudentInquiryDto
{
	public string FirstName { get; set; }	
	public string MiddleName { get; set; }
	public string LastName { get; set; }
	public string FormNumber { get; set; }
	public string PreviousSchoolName { get; set; }
	public DateTimeOffset DateOfBirth { get; set; }
	public int Age { get; set; }
	public DateTimeOffset DateOfInquiry { get; set; }
	public GenderType GenderType { get; set; }
	public string ParentContactNumber { get; set; }
	public string AlternativeContactNumber { get; set; }
	public string City { get; set; }
	public string State { get; set; }
	public int PinCode { get; set; }
	public string Address { get; set; }
	public string InquiryPreferences { get; set; }
	
	// value Object
	public Guid EducationMediumId { get; set; }
	public Guid AcademicYearId { get; set; }
	public Guid StanderdId { get; set; }
	
}