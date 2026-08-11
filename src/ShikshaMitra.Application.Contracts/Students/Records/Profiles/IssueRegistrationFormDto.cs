using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.Records.Profiles;

public class IssueRegistrationFormDto
{
	/// <summary>
	/// Registration form number - must be unique
	/// </summary>
	[Required(ErrorMessage = "Form number is required")]
	[StringLength(50, MinimumLength = 3, ErrorMessage = "Form number must be between 3 and 50 characters")]
	[RegularExpression(@"^[A-Z0-9\-]+$", ErrorMessage = "Form number must contain only uppercase letters, numbers, and hyphens")]
	public required string FormNumber { get; set; }

	/// <summary>
	/// Registration form amount/fee
	/// </summary>
	[Required(ErrorMessage = "Amount is required")]
	[Range(0.01, 999999.99, ErrorMessage = "Amount must be between 0.01 and 999,999.99")]
	public required decimal Amount { get; set; }
}
