using System;

namespace ShikshaMitra.Students.StudentEnrollments.Registration;

public class StudentContactDetailsDto
{
	public string? Name { get;  set; }
	public DateTimeOffset? DateOfBirth { get;  set; }
	public string? Occupation { get;  set; }
	public string? Qualification { get;  set; }
	public string? ContactNo { get;  set; }
	public string? Email { get;  set; }
	public int? PassOutYear { get;  set; }
	public string? OfficeNo { get;  set; }
	public string? OfficeAddress { get;  set; }
	public string? Designation { get;  set; }
	public string? Organisation { get;  set; }
	public string? AadhaarNo { get;  set; }
	public string? Remarks { get;  set; }
	public string? Address { get;  set; }
}