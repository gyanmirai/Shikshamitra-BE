using ShikshaMitra.Common;

namespace ShikshaMitra.Students.StudentEnrollments.Registration;

public class StudentAdditionalDetailsDto
{
	public Category? Category { get; set; }
	public bool? RightToEducation { get; set; }
	public bool? BPLStudent { get; set; }
	public string? BPLCardNo { get; set; }
	public bool? PersonWithDisability { get; set; }
	public string? SecondLanguage { get; set; }
	public string? EmergencyContactNo { get; set; }
	public bool? SingleParentChild { get; set; }
	public bool? SingleChild { get; set; }
	public string? SponsorName { get; set; }
}