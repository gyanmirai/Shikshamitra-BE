namespace ShikshaMitra.Students.StudentEnrollments.Registration;

public class UpdateStudentFamilyDetailsDto
{
	public StudentContactDetailsDto FatherDetails { get; set; }
	public StudentContactDetailsDto MotherDetails { get; set; }
	public StudentContactDetailsDto GuardianDetails { get; set; }
}