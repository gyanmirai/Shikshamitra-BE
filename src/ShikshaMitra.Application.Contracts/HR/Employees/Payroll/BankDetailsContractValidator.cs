using FluentValidation;

namespace ShikshaMitra.HR.Employees.Payroll;

public class BankDetailsContractValidator : AbstractValidator<BankDetailsContract>
{
	private const int MaxBankNameLength = 100;
	private const int MaxBranchNameLength = 100;
	private const int MaxAccountHolderNameLength = 150;
	private const int MinAccountNumberLength = 8;
	private const int MaxAccountNumberLength = 20;
	private const string IfscCodePattern = @"^[A-Z]{4}0[A-Z0-9]{6}$";

	public BankDetailsContractValidator()
	{
		RuleFor(x => x.BankName)
			.NotEmpty().WithMessage("Bank name is required when bank details are provided.")
			.MaximumLength(MaxBankNameLength).WithMessage($"Bank name must not exceed {MaxBankNameLength} characters.");

		RuleFor(x => x.BranchName)
			.MaximumLength(MaxBranchNameLength).WithMessage($"Branch name must not exceed {MaxBranchNameLength} characters.")
			.When(x => x.BranchName != null);

		RuleFor(x => x.AccountHolderName)
			.NotEmpty().WithMessage("Account holder name is required when bank details are provided.")
			.MaximumLength(MaxAccountHolderNameLength).WithMessage($"Account holder name must not exceed {MaxAccountHolderNameLength} characters.");

		RuleFor(x => x.AccountNumber)
			.NotEmpty().WithMessage("Account number is required when bank details are provided.")
			.MinimumLength(MinAccountNumberLength).WithMessage($"Account number must be at least {MinAccountNumberLength} characters.")
			.MaximumLength(MaxAccountNumberLength).WithMessage($"Account number must not exceed {MaxAccountNumberLength} characters.")
			.Matches(@"^\d+$").WithMessage("Account number must contain only digits.");

		RuleFor(x => x.IfscCode)
			.NotEmpty().WithMessage("IFSC code is required when bank details are provided.")
			.Matches(IfscCodePattern).WithMessage("IFSC code must follow the format: 4 uppercase letters, a zero, then 6 alphanumeric characters (e.g., SBIN0001234).");
	}
}
