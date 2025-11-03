using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments;
public class IssueRegistrationFormDto
{
	[Required]
	public required string FormNumber { get; set; }
	[Required]
	public required double Amount { get; set; }
}
