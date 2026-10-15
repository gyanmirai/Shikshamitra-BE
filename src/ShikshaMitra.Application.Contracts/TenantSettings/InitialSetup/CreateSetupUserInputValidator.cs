using FluentValidation;

namespace ShikshaMitra.TenantSettings.InitialSetup;

public class CreateSetupUserInputValidator : AbstractValidator<CreateSetupUserInput>
{
	public CreateSetupUserInputValidator()
	{
		RuleFor(input => input.UserName)
			.NotEmpty()
			.MinimumLength(3)
			.WithMessage("Username must be at least 3 characters long and contain no spaces.")
			.Matches(@"^\S+$")
			.WithMessage("Username must be at least 3 characters long and contain no spaces.");
		RuleFor(input => input.Password)
			.MinimumLength(8)
			.WithMessage("Use at least 8 characters");
	}
}
