using System;

namespace ShikshaMitra.Students;

public class StudentParentDetailsDto
{
	public string? Name { get; set; }
	public DateOnly? DateOfBirth { get; set; }
	public string? Occupation { get; set; }
	public string? Qualification { get; set; }
	public string? ExStudent { get; set; }
	public int? PassOutYear { get; set; }
	public string? Organization { get; set; }
	public string? Designation { get; set; }
	public string? OfficeAddress { get; set; }
	public string? ContactNumber { get; set; }
	public string? OfficeContactNumber { get; set; }
	public string? Email { get; set; }
	public string? AadharNo { get; set; }
	public decimal? AnnualIncome { get; set; }
}