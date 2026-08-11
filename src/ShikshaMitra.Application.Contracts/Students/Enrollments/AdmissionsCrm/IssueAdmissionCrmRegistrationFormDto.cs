using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class IssueAdmissionCrmRegistrationFormDto
{
	[Required(ErrorMessage = "Form number is required")]
	[StringLength(
		AdmissionCrmConsts.MaxRegistrationFormNumberLength,
		MinimumLength = 3,
		ErrorMessage = "Form number must be between 3 and 50 characters")]
	[RegularExpression(
		@"^[A-Z0-9\-]+$",
		ErrorMessage = "Form number must contain only uppercase letters, numbers, and hyphens")]
	public required string FormNumber { get; set; }

	[Required(ErrorMessage = "Amount is required")]
	[Range(0.01, 999999.99, ErrorMessage = "Amount must be between 0.01 and 999,999.99")]
	public required decimal Amount { get; set; }
}
