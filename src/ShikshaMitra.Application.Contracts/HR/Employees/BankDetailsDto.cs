using FluentValidation;

namespace ShikshaMitra.HR.Employees;

public class BankDetailsDto
{
	public string? BankName { get; set; }

	public string? BranchName { get; set; }

	public string? AccountHolderName { get; set; }

	public string? AccountNumber { get; set; }

	public string? IfscCode { get; set; }
}

public class BankDetailsDtoValidator : AbstractValidator<BankDetailsDto>
{
	public BankDetailsDtoValidator()
	{
		RuleFor(x => x.BankName)
			.MaximumLength(256)
			.Matches(@"^[a-zA-Z0-9\s\-\.]+$")
			.WithMessage("Bank name contains invalid characters")
			.When(x => !string.IsNullOrWhiteSpace(x.BankName));

		RuleFor(x => x.BranchName)
			.MaximumLength(256)
			.Matches(@"^[a-zA-Z0-9\s\-\.]+$")
			.WithMessage("Branch name contains invalid characters")
			.When(x => !string.IsNullOrWhiteSpace(x.BranchName));

		RuleFor(x => x.AccountHolderName)
			.MaximumLength(256)
			.Matches(@"^[a-zA-Z\s\.]+$")
			.WithMessage("Account holder name should only contain letters")
			.When(x => !string.IsNullOrWhiteSpace(x.AccountHolderName));

		RuleFor(x => x.AccountNumber)
			.MaximumLength(20)
			.Matches(@"^\d{9,18}$")
			.WithMessage("Account number must be between 9 and 18 digits")
			.When(x => !string.IsNullOrWhiteSpace(x.AccountNumber));

		RuleFor(x => x.IfscCode)
			.MaximumLength(11)
			.Matches(@"^[A-Z]{4}0[A-Z0-9]{6}$")
			.WithMessage("Invalid IFSC code format. Example: SBIN0001234")
			.When(x => !string.IsNullOrWhiteSpace(x.IfscCode));
	}
}
