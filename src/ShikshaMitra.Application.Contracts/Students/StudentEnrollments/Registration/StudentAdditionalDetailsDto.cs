using ShikshaMitra.Common;

namespace ShikshaMitra.Students.StudentEnrollments.Registration;

public class StudentAdditionalDetailsDto
{
	public Category? Category { get; set; }
	public YesNoStatus? RightToEducation { get; set; }
	public YesNoStatus? BPLStudent { get; set; }
	public string? BPLCardNo { get; set; }
	public YesNoStatus? PersonWithDisability { get; set; }
	public string? SecondLanguage { get; set; }
	public string? EmergencyContactNo { get; set; }
	public YesNoStatus? SingleParentChild { get; set; }
	public YesNoStatus? SingleChild { get; set; }
	public string? SponsorName { get; set; }
}