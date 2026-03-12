using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.Employees;

public class BankDetailsDto
{
	[MaxLength(256)]
	[RegularExpression(@"^[a-zA-Z0-9\s\-\.]+$", ErrorMessage = "Bank name contains invalid characters")]
	public string? BankName { get; set; }

	[MaxLength(256)]
	[RegularExpression(@"^[a-zA-Z0-9\s\-\.]+$", ErrorMessage = "Branch name contains invalid characters")]
	public string? BranchName { get; set; }

	[MaxLength(256)]
	[RegularExpression(@"^[a-zA-Z\s\.]+$", ErrorMessage = "Account holder name should only contain letters")]
	public string? AccountHolderName { get; set; }

	[MaxLength(20)]
	[RegularExpression(@"^\d{9,18}$", ErrorMessage = "Account number must be between 9 and 18 digits")]
	public string? AccountNumber { get; set; }

	[MaxLength(11)]
	[RegularExpression(@"^[A-Z]{4}0[A-Z0-9]{6}$", ErrorMessage = "Invalid IFSC code format. Example: SBIN0001234")]
	public string? IfscCode { get; set; }
}