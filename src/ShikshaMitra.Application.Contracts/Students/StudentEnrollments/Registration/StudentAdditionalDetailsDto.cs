namespace ShikshaMitra.Students.StudentEnrollments.Registration;

public class StudentAdditionalDetailsDto
{
	public Category? Category { get; set; }
	public RightToEducation? RightToEducation { get; set; }
	public BPLStudent? BPLStudent { get; set; }
	public string? BPLCardNo { get; set; }
	public PersonWithDisability? PersonWithDisability { get; set; }
	public string? SecondLanguage { get; set; }
	public string? EmergencyContactNo { get; set; }
	public SingleParentChild? SingleParentChild { get; set; }
	public SingleChild? SingleChild { get; set; }
	public string? SponsorName { get; set; }
}