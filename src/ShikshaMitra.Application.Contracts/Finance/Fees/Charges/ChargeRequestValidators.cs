using FluentValidation;
using ShikshaMitra.Finance.Fees.Charges.Dtos;

namespace ShikshaMitra.Finance.Fees.Charges;

public class CreateChargeRequestValidator : AbstractValidator<CreateChargeRequest>
{
	public CreateChargeRequestValidator()
	{
		RuleFor(x => x.ChargeName)
			.NotEmpty()
			.MinimumLength(3)
			.MaximumLength(100);

		RuleFor(x => x.ChargeCategoryId).NotEmpty();

		RuleFor(x => x.PurposeDescription)
			.NotEmpty()
			.MinimumLength(10)
			.MaximumLength(1000);

		RuleFor(x => x.Amount)
			.GreaterThan(0);

		RuleFor(x => x.DueDate)
			.Must(d => d.Date >= System.DateTime.UtcNow.Date)
			.WithMessage("Due date must not be in the past.");

		RuleFor(x => x.Priority)
			.InclusiveBetween(0, 2);

		RuleFor(x => x.AdditionalNotes)
			.MaximumLength(2000)
			.When(x => x.AdditionalNotes != null);

		RuleFor(x => x)
			.Must(x => x.Audience is not null || x.StudentId.HasValue)
			.WithMessage("Either Audience or StudentId must be provided.");

		RuleFor(x => x.Audience!)
			.SetValidator(new ChargeAudienceValidator())
			.When(x => x.Audience is not null);
	}
}

public class UpdateChargeRequestValidator : AbstractValidator<UpdateChargeRequest>
{
	public UpdateChargeRequestValidator()
	{
		RuleFor(x => x.ConcurrencyStamp).NotEmpty().MaximumLength(40);
		RuleFor(x => x.ChargeName)
			.MinimumLength(3)
			.MaximumLength(100)
			.When(x => x.ChargeName != null);

		RuleFor(x => x.PurposeDescription)
			.MinimumLength(10)
			.MaximumLength(1000)
			.When(x => x.PurposeDescription != null);

		RuleFor(x => x.Amount)
			.GreaterThan(0)
			.When(x => x.Amount.HasValue);

		RuleFor(x => x.DueDate)
			.Must(d => d!.Value.Date >= System.DateTime.UtcNow.Date)
			.When(x => x.DueDate.HasValue);

		RuleFor(x => x.Priority)
			.InclusiveBetween(0, 2)
			.When(x => x.Priority.HasValue);

		RuleFor(x => x.AdditionalNotes)
			.MaximumLength(2000)
			.When(x => x.AdditionalNotes != null);

		RuleFor(x => x.Audience!)
			.SetValidator(new ChargeAudienceValidator())
			.When(x => x.Audience is not null);
	}
}

public class ApproveChargeRequestValidator : AbstractValidator<ApproveChargeRequest>
{
	public ApproveChargeRequestValidator()
	{
		RuleFor(x => x.ConcurrencyStamp).NotEmpty().MaximumLength(40);
		RuleFor(x => x.ApprovalNotes)
			.MaximumLength(2000)
			.When(x => x.ApprovalNotes != null);
	}
}

public class RejectChargeRequestValidator : AbstractValidator<RejectChargeRequest>
{
	public RejectChargeRequestValidator()
	{
		RuleFor(x => x.ConcurrencyStamp).NotEmpty().MaximumLength(40);
		RuleFor(x => x.RejectionReason)
			.NotEmpty()
			.MinimumLength(10)
			.MaximumLength(2000);
	}
}

public class VoidChargeRequestValidator : AbstractValidator<VoidChargeRequest>
{
	public VoidChargeRequestValidator()
	{
		RuleFor(x => x.ConcurrencyStamp).NotEmpty().MaximumLength(40);
		RuleFor(x => x.Reason).NotEmpty().MinimumLength(10).MaximumLength(2000);
	}
}
