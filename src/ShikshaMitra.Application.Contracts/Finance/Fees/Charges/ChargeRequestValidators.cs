using FluentValidation;
using ShikshaMitra.Finance.Fees.Charges.Dtos;

namespace ShikshaMitra.Finance.Fees.Charges;

public class CreateChargeRequestValidator : AbstractValidator<CreateChargeRequest>
{
	public CreateChargeRequestValidator()
	{
		RuleFor(x => x.ChargeCategoryId).NotEmpty();

		RuleFor(x => x.StudentId).NotEmpty();

		RuleFor(x => x.PurposeDescription)
			.NotEmpty()
			.MinimumLength(10)
			.MaximumLength(1000);

		RuleFor(x => x.Amount)
			.GreaterThan(0)
			.WithMessage("Amount must be greater than 0.");

		RuleFor(x => x.DueDate)
			.Must(d => d.Date >= System.DateTime.UtcNow.Date)
			.WithMessage("Due date must not be in the past.");

		RuleFor(x => x.Priority)
			.InclusiveBetween(0, 2)
			.WithMessage("Priority must be 0 (Low), 1 (Medium), or 2 (High).");

		RuleFor(x => x.AdditionalNotes)
			.MaximumLength(2000)
			.When(x => x.AdditionalNotes != null);
	}
}

public class UpdateChargeRequestValidator : AbstractValidator<UpdateChargeRequest>
{
	public UpdateChargeRequestValidator()
	{
		RuleFor(x => x.PurposeDescription)
			.MinimumLength(10)
			.MaximumLength(1000)
			.When(x => x.PurposeDescription != null);

		RuleFor(x => x.Amount)
			.GreaterThan(0)
			.WithMessage("Amount must be greater than 0.")
			.When(x => x.Amount.HasValue);

		RuleFor(x => x.DueDate)
			.Must(d => d!.Value.Date >= System.DateTime.UtcNow.Date)
			.WithMessage("Due date must not be in the past.")
			.When(x => x.DueDate.HasValue);

		RuleFor(x => x.Priority)
			.InclusiveBetween(0, 2)
			.WithMessage("Priority must be 0 (Low), 1 (Medium), or 2 (High).")
			.When(x => x.Priority.HasValue);

		RuleFor(x => x.AdditionalNotes)
			.MaximumLength(2000)
			.When(x => x.AdditionalNotes != null);
	}
}

public class ApproveChargeRequestValidator : AbstractValidator<ApproveChargeRequest>
{
	public ApproveChargeRequestValidator()
	{
		RuleFor(x => x.ApprovalNotes)
			.MaximumLength(2000)
			.When(x => x.ApprovalNotes != null);
	}
}

public class RejectChargeRequestValidator : AbstractValidator<RejectChargeRequest>
{
	public RejectChargeRequestValidator()
	{
		RuleFor(x => x.RejectionReason)
			.NotEmpty()
			.MinimumLength(10)
			.MaximumLength(2000);
	}
}
