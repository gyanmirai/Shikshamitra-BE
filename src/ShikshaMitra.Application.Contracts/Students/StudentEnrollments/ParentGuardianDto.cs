using System;

namespace ShikshaMitra.Students.StudentEnrollments;

/// <summary>
/// DTO representing parent or guardian details
/// </summary>
public class ParentGuardianDto
{
	public string? FullName { get; set; }
	public DateTimeOffset? DateOfBirth { get; set; }
	public string? Occupation { get; set; }
	public string? Email { get; set; }
	public string? ContactNo { get; set; }
	public string? OfficeNo { get; set; }
	public string? Qualification { get; set; }
	public string? YearOfPassing { get; set; }
	public string? Designation { get; set; }
	public string? Organization { get; set; }
	public string? AadharNo { get; set; }

	// Office Address
	public AddressDto? OfficeAddress { get; set; }

	// Local Address
	public AddressDto? LocalAddress { get; set; }
}
